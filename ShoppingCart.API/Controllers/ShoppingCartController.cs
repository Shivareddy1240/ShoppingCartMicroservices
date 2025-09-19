using Microsoft.AspNetCore.Mvc;
using ShoppingCart.API.Models.Dtos;
using ShoppingCart.API.Services;

namespace ShoppingCart.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShoppingCartController : ControllerBase
{
    private readonly ICartService _cartService;

    public ShoppingCartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<CartDto>> GetCart(string userId)
    {
        var cart = await _cartService.GetCartAsync(userId);
        return Ok(cart);
    }

    [HttpPost("{userId}/add")]
    public async Task<ActionResult> AddItem(string userId, [FromBody] AddCartItemRequest request)
    {
        await _cartService.AddItemAsync(userId, request);
        return Ok();
    }

    [HttpDelete("{userId}/remove/{productId}")]
    public async Task<ActionResult> RemoveItem(string userId, string productId)
    {
        await _cartService.RemoveItemAsync(userId, productId);
        return Ok();
    }

    [HttpPost("{userId}/checkout")]
    public async Task<ActionResult> Checkout(string userId)
    {
        await _cartService.CheckoutAsync(userId);
        return Ok("Cart checked out successfully");
    }
}