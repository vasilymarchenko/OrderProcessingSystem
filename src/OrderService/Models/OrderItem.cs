namespace OrderService.Models;

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    
    // Navigation property
    public Order Order { get; set; } = null!;
}
