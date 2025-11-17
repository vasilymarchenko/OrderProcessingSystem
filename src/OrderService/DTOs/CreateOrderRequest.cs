using OrderProcessingSystem.Shared.Events;

namespace OrderService.Application.DTOs;

public record CreateOrderRequest(
    string CustomerEmail,
    IReadOnlyList<OrderItemDto> Items
);
