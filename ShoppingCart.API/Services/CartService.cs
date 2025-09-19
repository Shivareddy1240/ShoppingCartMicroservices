using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShoppingCart.API.Data;
using ShoppingCart.API.Models;
using ShoppingCart.API.Models.Dtos;
using System.Text.Json;

namespace ShoppingCart.API.Services;

public class CartService : ICartService
{
    private readonly CartContext _context;
    private readonly IDistributedCache _cache;
    private readonly IMessageQueueService _queueService;
    private readonly ILogger<CartService> _logger;

    public CartService(CartContext context, IDistributedCache cache, IMessageQueueService queueService, ILogger<CartService> logger)
    {
        _context = context;
        _cache = cache;
        _queueService = queueService;
        _logger = logger;
    }

    public async Task<CartDto> GetCartAsync(string userId)
    {
        _logger.LogDebug("Attempting to fetch cart for user {UserId} from Redis", userId);
        var cached = await _cache.GetStringAsync(userId);
        if (!string.IsNullOrEmpty(cached))
        {
            try
            {
                var deserializedCartDto = JsonSerializer.Deserialize<CartDto>(cached) ?? new CartDto { UserId = userId };
                _logger.LogDebug("Retrieved cart from Redis for user {UserId}: {Cart}", userId, cached);
                return deserializedCartDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize cart from Redis for user {UserId}", userId);
            }
        }

        _logger.LogDebug("No cart found in Redis for user {UserId}, querying database", userId);
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created new cart for user {UserId} in database", userId);
        }

        var cartDto = MapToCartDto(cart);
        try
        {
            await _cache.SetStringAsync(userId, JsonSerializer.Serialize(cartDto), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });
            _logger.LogDebug("Cached cart for user {UserId} in Redis", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache cart for user {UserId} in Redis", userId);
        }

        return cartDto;
    }

    public async Task AddItemAsync(string userId, AddCartItemRequest request)
    {
        _logger.LogDebug("Adding item to cart for user {UserId}: {Item}", userId, JsonSerializer.Serialize(request));
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created new cart for user {UserId} in database", userId);
        }

        var existing = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (existing != null)
        {
            existing.Quantity += request.Quantity;
            _context.Update(existing);
            _logger.LogDebug("Updated existing item {ProductId} for user {UserId}", request.ProductId, userId);
        }
        else
        {
            var newItem = new CartItem
            {
                ProductId = request.ProductId,
                ProductName = request.ProductName,
                Quantity = request.Quantity,
                Price = request.Price,
                CartId = cart.Id
            };
            cart.Items.Add(newItem);
            _context.Add(newItem);
            _logger.LogDebug("Added new item {ProductId} to cart for user {UserId}", request.ProductId, userId);
        }

        await _context.SaveChangesAsync();
        var cartDto = MapToCartDto(cart);
        try
        {
            await _cache.SetStringAsync(userId, JsonSerializer.Serialize(cartDto));
            _logger.LogDebug("Updated Redis cache for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Redis cache for user {UserId}", userId);
        }
    }

    public async Task RemoveItemAsync(string userId, string productId)
    {
        _logger.LogDebug("Removing item {ProductId} from cart for user {UserId}", productId, userId);
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart == null)
        {
            _logger.LogWarning("No cart found for user {UserId}", userId);
            return;
        }

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            cart.Items.Remove(item);
            _context.Remove(item);
            await _context.SaveChangesAsync();
            _logger.LogDebug("Removed item {ProductId} from cart for user {UserId}", productId, userId);

            var cartDto = MapToCartDto(cart);
            await _cache.SetStringAsync(userId, JsonSerializer.Serialize(cartDto));
            _logger.LogDebug("Updated Redis cache for user {UserId}", userId);
        }
    }

    public async Task CheckoutAsync(string userId)
    {
        _logger.LogDebug("Processing checkout for user {UserId}", userId);
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart != null && cart.Items.Any())
        {
            await _queueService.PublishCheckoutEventAsync(cart);
            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync();
            await _cache.RemoveAsync(userId);
            _logger.LogInformation("Checked out cart for user {UserId}, removed from database and cache", userId);
        }
        else
        {
            _logger.LogWarning("No cart or items found for checkout for user {UserId}", userId);
        }
    }

    private CartDto MapToCartDto(Cart cart)
    {
        return new CartDto
        {
            UserId = cart.UserId,
            Items = cart.Items.Select(i => new CartItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList()
        };
    }
}