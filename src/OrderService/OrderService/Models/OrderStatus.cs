namespace OrderService.Models;

public enum OrderStatus
{
    Pending,
    InventoryReserved,
    InventoryInsufficient,
    Failed
}
