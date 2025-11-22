using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Shared.Events;
using InventoryService.Application.Interfaces;

namespace InventoryService.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IInventoryRepository repository,
        ILogger<InventoryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<InventoryCheckResult> CheckAndReserveInventoryAsync(
        OrderPlacedEvent orderEvent, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking inventory for order {OrderId}", orderEvent.OrderId);

        var result = new InventoryCheckResult { IsSuccessful = true };
        var productCodes = orderEvent.Items.Select(i => i.ProductCode).ToList();

        // Fetch all inventory items at once
        var inventoryItems = await _repository.GetByProductCodesAsync(productCodes, cancellationToken);
        var inventoryDict = inventoryItems.ToDictionary(i => i.ProductCode);

        // Check inventory for each order item
        foreach (var orderItem in orderEvent.Items)
        {
            if (!inventoryDict.TryGetValue(orderItem.ProductCode, out var inventoryItem))
            {
                result.IsSuccessful = false;
                result.MissingItems.Add(orderItem.ProductCode);
                _logger.LogWarning("Product {ProductCode} not found in inventory", orderItem.ProductCode);
                continue;
            }

            if (inventoryItem.AvailableQuantity < orderItem.Quantity)
            {
                result.IsSuccessful = false;
                result.MissingItems.Add(orderItem.ProductCode);
                _logger.LogWarning("Product {ProductCode} - insufficient inventory (requested: {Requested}, available: {Available})",
                    orderItem.ProductCode, orderItem.Quantity, inventoryItem.AvailableQuantity);
            }
        }

        // If all checks passed, reserve inventory
        if (result.IsSuccessful)
        {
            foreach (var orderItem in orderEvent.Items)
            {
                var inventoryItem = inventoryDict[orderItem.ProductCode];
                inventoryItem.AvailableQuantity -= orderItem.Quantity;
                inventoryItem.LastUpdated = DateTime.UtcNow;

                await _repository.UpdateAsync(inventoryItem, cancellationToken);

                _logger.LogInformation("Reserved {Quantity} units of {ProductCode} (remaining: {Remaining})",
                    orderItem.Quantity, orderItem.ProductCode, inventoryItem.AvailableQuantity);
            }

            _logger.LogInformation("Order {OrderId} - inventory reserved successfully", orderEvent.OrderId);
        }
        else
        {
            _logger.LogWarning("Order {OrderId} - insufficient inventory for products: {Products}",
                orderEvent.OrderId, string.Join(", ", result.MissingItems));
        }

        return result;
    }
}
