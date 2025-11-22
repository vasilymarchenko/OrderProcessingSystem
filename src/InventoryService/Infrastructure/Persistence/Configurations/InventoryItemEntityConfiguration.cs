using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InventoryService.Infrastructure.Persistence.Entities;

namespace InventoryService.Infrastructure.Persistence.Configurations;

public class InventoryItemEntityConfiguration : IEntityTypeConfiguration<InventoryItemEntity>
{
    public void Configure(EntityTypeBuilder<InventoryItemEntity> builder)
    {
        builder.ToTable("InventoryItems");
        
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.AvailableQuantity)
            .IsRequired();

        builder.Property(i => i.LastUpdated)
            .IsRequired();

        // Unique index on ProductCode
        builder.HasIndex(i => i.ProductCode)
            .IsUnique();

        // Seed data
        builder.HasData(
            new InventoryItemEntity
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ProductCode = "PROD-001",
                AvailableQuantity = 100,
                LastUpdated = new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new InventoryItemEntity
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                ProductCode = "PROD-002",
                AvailableQuantity = 50,
                LastUpdated = new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new InventoryItemEntity
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                ProductCode = "PROD-003",
                AvailableQuantity = 0,
                LastUpdated = new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
