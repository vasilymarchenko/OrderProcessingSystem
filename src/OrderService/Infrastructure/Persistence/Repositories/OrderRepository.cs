using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interfaces;
using OrderService.Application.Models;
using OrderService.Infrastructure.Persistence.Entities;
using OrderService.Models;

namespace OrderService.Infrastructure.Persistence.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;

    public OrderRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(order);
        
        _context.Orders.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        
        return MapToDomain(entity);
    }

    public async Task<Order> AddOrderWithOutboxAsync(Order order, OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(order);
        
        _context.Orders.Add(entity);
        _context.OutboxMessages.Add(outboxMessage);
        
        await _context.SaveChangesAsync(cancellationToken);
        
        return MapToDomain(entity);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<(IReadOnlyList<Order> Orders, bool HasMore)> GetPagedAsync(
        DateTime? cursorTimestamp,
        Guid? cursorId,
        int pageSize,
        string? customerEmail = null,
        OrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders.Include(o => o.Items).AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            query = query.Where(o => o.CustomerEmail == customerEmail);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value.ToString());
        }

        // Apply cursor-based pagination
        if (cursorTimestamp.HasValue && cursorId.HasValue)
        {
            query = query.Where(o =>
                o.CreatedAt < cursorTimestamp.Value ||
                (o.CreatedAt == cursorTimestamp.Value && o.Id.CompareTo(cursorId.Value) < 0));
        }

        // Order by CreatedAt descending, then by Id for consistent ordering
        query = query.OrderByDescending(o => o.CreatedAt)
                     .ThenByDescending(o => o.Id);

        // Fetch one extra to determine if there are more results
        var entities = await query
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        // Determine if there are more results
        var hasMore = entities.Count > pageSize;
        var entitiesToReturn = hasMore ? entities.Take(pageSize).ToList() : entities;

        var orders = entitiesToReturn.Select(MapToDomain).ToList();

        return (orders, hasMore);
    }

    private static OrderEntity MapToEntity(Order order)
    {
        return new OrderEntity
        {
            Id = order.Id,
            CustomerEmail = order.CustomerEmail,
            Status = order.Status.ToString(),
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(i => new OrderItemEntity
            {
                Id = i.Id,
                ProductCode = i.ProductCode,
                Quantity = i.Quantity
            }).ToList()
        };
    }

    private static Order MapToDomain(OrderEntity entity)
    {
        return new Order
        {
            Id = entity.Id,
            CustomerEmail = entity.CustomerEmail,
            Status = Enum.Parse<OrderStatus>(entity.Status),
            CreatedAt = entity.CreatedAt,
            Items = entity.Items.Select(i => new OrderItem
            {
                Id = i.Id,
                ProductCode = i.ProductCode,
                Quantity = i.Quantity
            }).ToList()
        };
    }
}
