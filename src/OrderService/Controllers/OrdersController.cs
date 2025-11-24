using Microsoft.AspNetCore.Mvc;
using OrderProcessingSystem.Shared.Events;
using OrderService.Application.DTOs;
using OrderService.Application.Interfaces;
using OrderService.Models;
using OrderService.API.Filters;

namespace OrderService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orderService,
        ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        // FluentValidation handles validation automatically
        var order = await _orderService.CreateOrderAsync(request, cancellationToken);
        var response = MapToResponse(order);
        
        return CreatedAtAction(nameof(GetOrder), new { id = response.Id }, response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponse>> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderByIdAsync(id, cancellationToken);

        if (order == null)
        {
            return NotFound($"Order with ID {id} not found.");
        }

        return MapToResponse(order);
    }

    [HttpGet]
    [ValidatePageSize]
    public async Task<ActionResult<PagedOrderResponse>> GetOrders(
        [FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? customerEmail = null,
        [FromQuery] OrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var (orders, nextCursor, hasMore) = await _orderService.GetOrdersAsync(
            cursor, 
            pageSize, 
            customerEmail, 
            status, 
            cancellationToken);

        var response = new PagedOrderResponse(
            Orders: orders.Select(MapToResponse).ToList(),
            NextCursor: nextCursor,
            HasMore: hasMore
        );

        return Ok(response);
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
