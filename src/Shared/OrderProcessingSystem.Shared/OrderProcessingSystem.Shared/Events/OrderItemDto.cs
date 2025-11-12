namespace OrderProcessingSystem.Shared.Events;

public class OrderItemDto
{
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
