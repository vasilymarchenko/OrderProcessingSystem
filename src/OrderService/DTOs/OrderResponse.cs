using OrderProcessingSystem.Shared.Events;
using OrderService.Models;

namespace OrderService.DTOs;

public record OrderResponse(
    Guid Id,
    string CustomerEmail,
    OrderStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items
);
