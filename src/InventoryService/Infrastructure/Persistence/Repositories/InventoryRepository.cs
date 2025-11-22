using Microsoft.EntityFrameworkCore;
using InventoryService.Application.Interfaces;
using InventoryService.Application.Models;
using InventoryService.Infrastructure.Persistence.Entities;

namespace InventoryService.Infrastructure.Persistence.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly InventoryDbContext _context;

    public InventoryRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryItem?> GetByProductCodeAsync(string productCode, CancellationToken cancellationToken = default)
    {
        var entity = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.ProductCode == productCode, cancellationToken);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<InventoryItem>> GetByProductCodesAsync(
        IEnumerable<string> productCodes, 
        CancellationToken cancellationToken = default)
    {
        var codes = productCodes.ToList();
        var entities = await _context.InventoryItems
            .Where(i => codes.Contains(i.ProductCode))
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task UpdateAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        var entity = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == item.Id, cancellationToken);

        if (entity == null)
            throw new InvalidOperationException($"Inventory item with ID {item.Id} not found");

        // Update entity from domain model
        entity.AvailableQuantity = item.AvailableQuantity;
        entity.LastUpdated = item.LastUpdated;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static InventoryItem MapToDomain(InventoryItemEntity entity)
    {
        return new InventoryItem
        {
            Id = entity.Id,
            ProductCode = entity.ProductCode,
            AvailableQuantity = entity.AvailableQuantity,
            LastUpdated = entity.LastUpdated
        };
    }
}
