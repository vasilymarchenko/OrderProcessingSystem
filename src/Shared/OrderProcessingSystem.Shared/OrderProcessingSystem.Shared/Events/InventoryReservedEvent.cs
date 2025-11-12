namespace OrderProcessingSystem.Shared.Events;

public class InventoryReservedEvent
{
    public Guid OrderId { get; set; }
    public DateTime Timestamp { get; set; }
}
