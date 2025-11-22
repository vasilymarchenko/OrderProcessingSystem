using Microsoft.EntityFrameworkCore;
using InventoryService.Infrastructure.Persistence.Entities;
using InventoryService.Infrastructure.Persistence.Configurations;

namespace InventoryService.Infrastructure.Persistence;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<InventoryItemEntity> InventoryItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configuration from separate file
        modelBuilder.ApplyConfiguration(new InventoryItemEntityConfiguration());
    }
}
