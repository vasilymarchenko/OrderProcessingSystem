namespace OrderService.Infrastructure.Persistence.Entities;

public class OrderEntity
{
    public Guid Id { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    // EF Navigation property
    public virtual ICollection<OrderItemEntity> Items { get; set; } = new List<OrderItemEntity>();
}
