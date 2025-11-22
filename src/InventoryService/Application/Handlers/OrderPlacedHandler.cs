using OrderProcessingSystem.Shared.Events;
using OrderProcessingSystem.Shared.Messaging;
using InventoryService.Application.Interfaces;

namespace InventoryService.Application.Handlers;

public class OrderPlacedHandler
{
    private readonly IInventoryService _inventoryService;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OrderPlacedHandler> _logger;

    public OrderPlacedHandler(
        IInventoryService inventoryService,
        IMessagePublisher publisher,
        ILogger<OrderPlacedHandler> logger)
    {
        _inventoryService = inventoryService;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(OrderPlacedEvent orderEvent)
    {
        _logger.LogInformation("Processing order {OrderId} for customer {CustomerEmail}",
            orderEvent.OrderId, orderEvent.CustomerEmail);

        // Delegate business logic to service
        var result = await _inventoryService.CheckAndReserveInventoryAsync(orderEvent);

        // Publish appropriate event based on result
        if (result.IsSuccessful)
        {
            var reservedEvent = new InventoryReservedEvent(
                OrderId: orderEvent.OrderId,
                Timestamp: DateTime.UtcNow
            );

            await _publisher.PublishAsync("inventory.reserved", reservedEvent);
            _logger.LogInformation("Published inventory.reserved event for order {OrderId}", orderEvent.OrderId);
        }
        else
        {
            var insufficientEvent = new InventoryInsufficientEvent(
                OrderId: orderEvent.OrderId,
                MissingItems: result.MissingItems,
                Timestamp: DateTime.UtcNow
            );

            await _publisher.PublishAsync("inventory.insufficient", insufficientEvent);
            _logger.LogWarning("Published inventory.insufficient event for order {OrderId}", orderEvent.OrderId);
        }
    }
}
