using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderProcessingSystem.Shared.Events;
using OrderProcessingSystem.Shared.Messaging;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _context;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        OrderDbContext context, 
        IMessagePublisher publisher,
        ILogger<OrdersController> logger)
    {
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            return BadRequest("Customer email is required.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest("Order must contain at least one item.");
        }

        // Create order entity
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

        // Save to database
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} created for customer {CustomerEmail}", 
            order.Id, order.CustomerEmail);

        // Publish OrderPlacedEvent to RabbitMQ
        var orderPlacedEvent = new OrderPlacedEvent(
            OrderId: order.Id,
            CustomerEmail: order.CustomerEmail,
            Items: order.Items.Select(i => new OrderItemDto(i.ProductCode, i.Quantity)).ToList(),
            Timestamp: DateTime.UtcNow
        );

        await _publisher.PublishAsync("order.placed", orderPlacedEvent);

        _logger.LogInformation("OrderPlacedEvent published for order {OrderId} with routing key 'order.placed'", 
            order.Id);

        // Map to response
        var response = MapToResponse(order);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponse>> GetOrder(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound($"Order with ID {id} not found.");
        }

        return MapToResponse(order);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetOrders(
        [FromQuery] string? customerEmail = null,
        [FromQuery] OrderStatus? status = null)
    {
        var query = _context.Orders.Include(o => o.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            query = query.Where(o => o.CustomerEmail == customerEmail);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders.Select(MapToResponse));
    }

    private static OrderResponse MapToResponse(Order order)
    {
        return new OrderResponse(
            order.Id,
            order.CustomerEmail,
            order.Status,
            order.CreatedAt,
            order.Items.Select(i => new OrderItemDto(i.ProductCode, i.Quantity)).ToList()
        );
    }
}
