namespace ShoppingCart.API.Models;

public class CartItem
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public int CartId { get; set; }
    public Cart? Cart { get; set; }
}