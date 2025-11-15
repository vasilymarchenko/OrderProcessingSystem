using Microsoft.EntityFrameworkCore;
using InventoryService.Models;

namespace InventoryService.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<InventoryItem> InventoryItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure InventoryItem entity
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.Property(i => i.ProductCode)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(i => i.AvailableQuantity)
                .IsRequired();

            entity.Property(i => i.LastUpdated)
                .IsRequired();

            // Unique index on ProductCode
            entity.HasIndex(i => i.ProductCode)
                .IsUnique();
        });

        // Seed initial inventory data
        modelBuilder.Entity<InventoryItem>().HasData(
            new InventoryItem
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ProductCode = "PROD-001",
                AvailableQuantity = 100,
                LastUpdated = new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new InventoryItem
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                ProductCode = "PROD-002",
                AvailableQuantity = 50,
                LastUpdated = new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new InventoryItem
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                ProductCode = "PROD-003",
                AvailableQuantity = 0,
                LastUpdated = new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
