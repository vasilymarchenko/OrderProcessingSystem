namespace OrderProcessingSystem.Shared.Events;

public record OrderItemDto(
    string ProductCode,
    int Quantity
);
