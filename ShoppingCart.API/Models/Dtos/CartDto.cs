namespace ShoppingCart.API.Models.Dtos;

public class CartDto
{
    public string UserId { get; set; } = string.Empty;
    public List<CartItemDto> Items { get; set; } = new();
}