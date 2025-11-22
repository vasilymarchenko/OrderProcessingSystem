using InventoryService.Application.Models;

namespace InventoryService.Application.Interfaces;

public interface IInventoryRepository
{
    Task<InventoryItem?> GetByProductCodeAsync(string productCode, CancellationToken cancellationToken = default);
    Task UpdateAsync(InventoryItem item, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryItem>> GetByProductCodesAsync(IEnumerable<string> productCodes, CancellationToken cancellationToken = default);
}
