namespace OrderService.Infrastructure.Persistence.Entities;

public class OrderItemEntity
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    
    // EF Navigation property
    public virtual OrderEntity Order { get; set; } = null!;
}
