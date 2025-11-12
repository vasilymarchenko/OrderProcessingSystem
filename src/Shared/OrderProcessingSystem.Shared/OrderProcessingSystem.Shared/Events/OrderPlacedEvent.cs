namespace OrderProcessingSystem.Shared.Events;

public record OrderPlacedEvent(
    Guid OrderId,
    string CustomerEmail,
    IReadOnlyList<OrderItemDto> Items,
    DateTime Timestamp
);
