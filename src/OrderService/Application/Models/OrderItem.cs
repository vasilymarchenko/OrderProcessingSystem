namespace OrderService.Application.Models;

public class OrderItem
{
    public Guid Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
