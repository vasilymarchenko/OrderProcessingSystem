namespace OrderProcessingSystem.Shared.Events;

public class OrderPlacedEvent
{
    public Guid OrderId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
