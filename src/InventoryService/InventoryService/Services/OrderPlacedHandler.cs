using Microsoft.EntityFrameworkCore;
using OrderProcessingSystem.Shared.Events;
using OrderProcessingSystem.Shared.Messaging;
using InventoryService.Data;

namespace InventoryService.Services;

public class OrderPlacedHandler
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OrderPlacedHandler> _logger;

    public OrderPlacedHandler(
        IServiceScopeFactory serviceScopeFactory,
        IMessagePublisher publisher,
        ILogger<OrderPlacedHandler> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(OrderPlacedEvent orderEvent)
    {
        _logger.LogInformation("Processing order {OrderId} for customer {CustomerEmail}",
            orderEvent.OrderId, orderEvent.CustomerEmail);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var missingItems = new List<string>();

        // Check inventory for each item
        foreach (var item in orderEvent.Items)
        {
            var inventoryItem = await context.InventoryItems
                .FirstOrDefaultAsync(i => i.ProductCode == item.ProductCode);

            if (inventoryItem == null || inventoryItem.AvailableQuantity < item.Quantity)
            {
                missingItems.Add(item.ProductCode);
                _logger.LogWarning("Product {ProductCode} - insufficient inventory (requested: {Requested}, available: {Available})",
                    item.ProductCode, item.Quantity, inventoryItem?.AvailableQuantity ?? 0);
            }
        }

        if (missingItems.Any())
        {
            // Insufficient inventory - publish failure event
            var insufficientEvent = new InventoryInsufficientEvent(
                OrderId: orderEvent.OrderId,
                MissingItems: missingItems,
                Timestamp: DateTime.UtcNow
            );

            await _publisher.PublishAsync("inventory.insufficient", insufficientEvent);
            _logger.LogWarning("Order {OrderId} - insufficient inventory for products: {Products}",
                orderEvent.OrderId, string.Join(", ", missingItems));
        }
        else
        {
            // Reserve inventory - update quantities
            foreach (var item in orderEvent.Items)
            {
                var inventoryItem = await context.InventoryItems
                    .FirstAsync(i => i.ProductCode == item.ProductCode);

                inventoryItem.AvailableQuantity -= item.Quantity;
                inventoryItem.LastUpdated = DateTime.UtcNow;

                _logger.LogInformation("Reserved {Quantity} units of {ProductCode} (remaining: {Remaining})",
                    item.Quantity, item.ProductCode, inventoryItem.AvailableQuantity);
            }

            await context.SaveChangesAsync();

            // Publish success event
            var reservedEvent = new InventoryReservedEvent(
                OrderId: orderEvent.OrderId,
                Timestamp: DateTime.UtcNow
            );

            await _publisher.PublishAsync("inventory.reserved", reservedEvent);
            _logger.LogInformation("Order {OrderId} - inventory reserved successfully", orderEvent.OrderId);
        }
    }
}
