using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Shared.Events;
using OrderProcessingSystem.Shared.Messaging;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Application.Models;

namespace OrderService.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository repository,
        IMessagePublisher publisher,
        ILogger<OrderService> logger)
    {
        _repository = repository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        // Create domain entity
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerEmail = request.CustomerEmail,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Items = request.Items.Select(i => new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductCode = i.ProductCode,
                Quantity = i.Quantity
            }).ToList()
        };

        // Save to repository
        order = await _repository.AddAsync(order, cancellationToken);

        _logger.LogInformation("Order {OrderId} created for customer {CustomerEmail}",
            order.Id, order.CustomerEmail);

        // Publish OrderPlacedEvent
        var orderPlacedEvent = new OrderPlacedEvent(
            OrderId: order.Id,
            CustomerEmail: order.CustomerEmail,
            Items: order.Items.Select(i => new OrderItemDto(i.ProductCode, i.Quantity)).ToList(),
            Timestamp: DateTime.UtcNow
        );

        await _publisher.PublishAsync("order.placed", orderPlacedEvent);

        _logger.LogInformation("OrderPlacedEvent published for order {OrderId} with routing key 'order.placed'",
            order.Id);

        return order;
    }

    public async Task<Order?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Order> Orders, string? NextCursor, bool HasMore)> GetOrdersAsync(
        string? cursor,
        int pageSize,
        string? customerEmail,
        OrderStatus? status,
        CancellationToken cancellationToken = default)
    {
        // Parse cursor (format: timestamp_id)
        DateTime? cursorTimestamp = null;
        Guid? cursorId = null;

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var parts = cursor.Split('_');
            if (parts.Length == 2 &&
                DateTime.TryParse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var timestamp) &&
                Guid.TryParse(parts[1], out var id))
            {
                cursorTimestamp = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();
                cursorId = id;
            }
            else
            {
                throw new ArgumentException("Invalid cursor format.", nameof(cursor));
            }
        }

        // Get paged results from repository
        var (orders, hasMore) = await _repository.GetPagedAsync(
            cursorTimestamp,
            cursorId,
            pageSize,
            customerEmail,
            status,
            cancellationToken);

        // Generate next cursor from the last item
        string? nextCursor = null;
        if (hasMore && orders.Any())
        {
            var lastOrder = orders.Last();
            nextCursor = $"{lastOrder.CreatedAt:O}_{lastOrder.Id}";
        }

        _logger.LogInformation(
            "Listed {Count} orders with cursor pagination (HasMore: {HasMore})",
            orders.Count, hasMore);

        return (orders, nextCursor, hasMore);
    }
}