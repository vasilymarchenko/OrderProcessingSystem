namespace OrderProcessingSystem.Shared.Events;

public record InventoryInsufficientEvent(
    Guid OrderId,
    IReadOnlyList<string> MissingItems,
    DateTime Timestamp
);
