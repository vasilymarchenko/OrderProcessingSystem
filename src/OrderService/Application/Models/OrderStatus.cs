namespace OrderService.Application.Models;

public enum OrderStatus
{
    Pending,
    InventoryReserved,
    InventoryInsufficient,
    Failed
}
