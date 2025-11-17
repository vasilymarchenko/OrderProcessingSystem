using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) 
        : base(options)
    {
    }

    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Order entity
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            
            entity.Property(o => o.CustomerEmail)
                .IsRequired()
                .HasMaxLength(256);
            
            entity.Property(o => o.Status)
                .IsRequired()
                .HasConversion<string>();
            
            entity.Property(o => o.CreatedAt)
                .IsRequired();

            // Index for querying by customer email
            entity.HasIndex(o => o.CustomerEmail);
            
            // Index for querying by status
            entity.HasIndex(o => o.Status);
            
            // Index for querying by created date
            entity.HasIndex(o => o.CreatedAt);
        });

        // Configure OrderItem entity
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(oi => oi.Id);
            
            entity.Property(oi => oi.ProductCode)
                .IsRequired()
                .HasMaxLength(100);
            
            entity.Property(oi => oi.Quantity)
                .IsRequired();

            // Configure relationship
            entity.HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for querying by product code
            entity.HasIndex(oi => oi.ProductCode);
        });
    }
}
