namespace Order.API.Models;

public class Order
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total => Items.Sum(i => i.Quantity * i.Price);
}
