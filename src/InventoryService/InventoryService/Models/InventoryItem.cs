namespace InventoryService.Models;

public class InventoryItem
{
    public Guid Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public DateTime LastUpdated { get; set; }
}
