using OrderProcessingSystem.Shared.Events;

namespace InventoryService.Application.Interfaces;

public interface IInventoryService
{
    Task<InventoryCheckResult> CheckAndReserveInventoryAsync(OrderPlacedEvent orderEvent, CancellationToken cancellationToken = default);
}

public class InventoryCheckResult
{
    public bool IsSuccessful { get; set; }
    public List<string> MissingItems { get; set; } = new();
}
