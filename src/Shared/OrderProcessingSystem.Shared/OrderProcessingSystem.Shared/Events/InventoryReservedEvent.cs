namespace OrderProcessingSystem.Shared.Events;

public record InventoryReservedEvent(
    Guid OrderId,
    DateTime Timestamp
);
