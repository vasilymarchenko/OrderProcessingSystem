using OrderProcessingSystem.Shared.Events;
using OrderService.Application.Models;

namespace OrderService.Application.DTOs;

public record OrderResponse(
    Guid Id,
    string CustomerEmail,
    OrderStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items
);
