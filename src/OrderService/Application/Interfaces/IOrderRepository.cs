using OrderService.Models;

namespace OrderService.Application.Interfaces;

public interface IOrderRepository
{
    Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default);
    Task<Order> AddOrderWithOutboxAsync(Order order, OutboxMessage outboxMessage, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Order> Orders, bool HasMore)> GetPagedAsync(
        DateTime? cursorTimestamp,
        Guid? cursorId,
        int pageSize,
        string? customerEmail = null,
        OrderStatus? status = null,
        CancellationToken cancellationToken = default);
}
