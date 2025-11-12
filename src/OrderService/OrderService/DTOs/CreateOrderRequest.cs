using OrderProcessingSystem.Shared.Events;

namespace OrderService.DTOs;

public record CreateOrderRequest(
    string CustomerEmail,
    IReadOnlyList<OrderItemDto> Items
);
