namespace OrderProcessingSystem.Shared.Events;

public class InventoryInsufficientEvent
{
    public Guid OrderId { get; set; }
    public List<string> MissingItems { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
