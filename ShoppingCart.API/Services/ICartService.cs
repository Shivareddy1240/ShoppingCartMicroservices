using ShoppingCart.API.Models;
using ShoppingCart.API.Models.Dtos;

public interface ICartService
{
    Task<CartDto> GetCartAsync(string userId);
    Task AddItemAsync(string userId, AddCartItemRequest request);
    Task RemoveItemAsync(string userId, string productId);
    Task CheckoutAsync(string userId);
}