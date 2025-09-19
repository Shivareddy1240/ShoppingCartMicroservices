namespace Order.API.Models;

public class OrderItem
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; } = null!;
}
