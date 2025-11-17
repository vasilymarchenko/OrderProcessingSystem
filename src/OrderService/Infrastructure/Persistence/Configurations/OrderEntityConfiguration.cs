using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Infrastructure.Persistence.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations;

public class OrderEntityConfiguration : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> builder)
    {
        builder.ToTable("Orders");
        
        builder.HasKey(o => o.Id);
        
        builder.Property(o => o.CustomerEmail)
            .IsRequired()
            .HasMaxLength(256);
        
        builder.Property(o => o.Status)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(o => o.CreatedAt)
            .IsRequired();

        // Indexes for querying
        builder.HasIndex(o => o.CustomerEmail);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CreatedAt);
    }
}
