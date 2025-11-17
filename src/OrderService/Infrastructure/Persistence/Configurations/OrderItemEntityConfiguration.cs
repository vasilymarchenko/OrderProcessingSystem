using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Infrastructure.Persistence.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations;

public class OrderItemEntityConfiguration : IEntityTypeConfiguration<OrderItemEntity>
{
    public void Configure(EntityTypeBuilder<OrderItemEntity> builder)
    {
        builder.ToTable("OrderItems");
        
        builder.HasKey(oi => oi.Id);
        
        builder.Property(oi => oi.ProductCode)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(oi => oi.Quantity)
            .IsRequired();

        // Configure relationship
        builder.HasOne(oi => oi.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for querying by product code
        builder.HasIndex(oi => oi.ProductCode);
    }
}
