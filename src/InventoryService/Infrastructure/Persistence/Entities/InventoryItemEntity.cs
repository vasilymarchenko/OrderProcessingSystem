namespace InventoryService.Infrastructure.Persistence.Entities;

// EF-specific model
public class InventoryItemEntity
{
    public Guid Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public DateTime LastUpdated { get; set; }
}
