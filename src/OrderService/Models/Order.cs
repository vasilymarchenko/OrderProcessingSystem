namespace OrderService.Models;

public class Order
{
    public Guid Id { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation property
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
