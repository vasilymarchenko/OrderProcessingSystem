using OrderService.Application.DTOs;
using OrderService.Models;

namespace OrderService.Application.Interfaces;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Order> Orders, string? NextCursor, bool HasMore)> GetOrdersAsync(
        string? cursor,
        int pageSize,
        string? customerEmail,
        OrderStatus? status,
        CancellationToken cancellationToken = default);
}
