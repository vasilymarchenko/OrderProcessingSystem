# InventoryService - Clean Architecture Refactoring Guide

## 🎯 Objective

Refactor InventoryService to follow the same Clean/Onion Architecture principles successfully implemented in OrderService.

## 📋 Current Issues

### ❌ Problems to Fix:
1. **Business logic in handler** - `OrderPlacedHandler` contains inventory checking, reservation logic, and event publishing
2. **Direct DbContext usage** - Handler directly accesses `InventoryDbContext`
3. **No abstraction layer** - Can't easily mock or swap implementations
4. **Mixed concerns** - Data access, business rules, and messaging all in one class
5. **Poor testability** - Hard to unit test without database

## 🏗️ Target Architecture

```
Application/
├── Interfaces/
│   ├── IInventoryRepository.cs          # Abstraction for data access
│   └── IInventoryService.cs             # Abstraction for business logic
├── Models/                              # Core domain models (clean POCOs)
│   └── InventoryItem.cs                 # Domain model (no EF)
├── Services/
│   └── InventoryService.cs              # Business logic (inventory checking, reservation)
└── Handlers/
    └── OrderPlacedHandler.cs            # Thin handler (delegates to service)

Infrastructure/
└── Persistence/
    ├── InventoryDbContext.cs            # EF DbContext
    ├── Entities/
    │   └── InventoryItemEntity.cs       # EF-specific model
    ├── Configurations/
    │   └── InventoryItemEntityConfiguration.cs  # Fluent API config
    └── Repositories/
        └── InventoryRepository.cs       # Repository with domain ↔ EF mapping
```

## 📝 Step-by-Step Implementation

### **Step 1: Create Application Layer Interfaces**

**File: `Application/Interfaces/IInventoryRepository.cs`**
```csharp
using InventoryService.Application.Models;

namespace InventoryService.Application.Interfaces;

public interface IInventoryRepository
{
    Task<InventoryItem?> GetByProductCodeAsync(string productCode, CancellationToken cancellationToken = default);
    Task UpdateAsync(InventoryItem item, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryItem>> GetByProductCodesAsync(IEnumerable<string> productCodes, CancellationToken cancellationToken = default);
}
```

**File: `Application/Interfaces/IInventoryService.cs`**
```csharp
using OrderProcessingSystem.Shared.Events;

namespace InventoryService.Application.Interfaces;

public interface IInventoryService
{
    Task<InventoryCheckResult> CheckAndReserveInventoryAsync(OrderPlacedEvent orderEvent, CancellationToken cancellationToken = default);
}

public class InventoryCheckResult
{
    public bool IsSuccessful { get; set; }
    public List<string> MissingItems { get; set; } = new();
}
```

---

### **Step 2: Move Domain Model**

**File: `Application/Models/InventoryItem.cs`**
```csharp
namespace InventoryService.Application.Models;

// Clean POCO - no EF concerns
public class InventoryItem
{
    public Guid Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public DateTime LastUpdated { get; set; }

    // Business logic can be added here later
    // public bool CanReserve(int quantity) => AvailableQuantity >= quantity;
    // public void Reserve(int quantity) { ... }
}
```

---

### **Step 3: Create EF Entities in Infrastructure**

**File: `Infrastructure/Persistence/Entities/InventoryItemEntity.cs`**
```csharp
namespace InventoryService.Infrastructure.Persistence.Entities;

// EF-specific model
public class InventoryItemEntity
{
    public Guid Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

**File: `Infrastructure/Persistence/Configurations/InventoryItemEntityConfiguration.cs`**
```csharp
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
```

---

### **Step 4: Update DbContext**

**File: `Infrastructure/Persistence/InventoryDbContext.cs`**
```csharp
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
```

---

### **Step 5: Create Repository Implementation**

**File: `Infrastructure/Persistence/Repositories/InventoryRepository.cs`**
```csharp
using Microsoft.EntityFrameworkCore;
using InventoryService.Application.Interfaces;
using InventoryService.Application.Models;
using InventoryService.Infrastructure.Persistence.Entities;

namespace InventoryService.Infrastructure.Persistence.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly InventoryDbContext _context;

    public InventoryRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryItem?> GetByProductCodeAsync(string productCode, CancellationToken cancellationToken = default)
    {
        var entity = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.ProductCode == productCode, cancellationToken);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<InventoryItem>> GetByProductCodesAsync(
        IEnumerable<string> productCodes, 
        CancellationToken cancellationToken = default)
    {
        var codes = productCodes.ToList();
        var entities = await _context.InventoryItems
            .Where(i => codes.Contains(i.ProductCode))
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task UpdateAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        var entity = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == item.Id, cancellationToken);

        if (entity == null)
            throw new InvalidOperationException($"Inventory item with ID {item.Id} not found");

        // Update entity from domain model
        entity.AvailableQuantity = item.AvailableQuantity;
        entity.LastUpdated = item.LastUpdated;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static InventoryItem MapToDomain(InventoryItemEntity entity)
    {
        return new InventoryItem
        {
            Id = entity.Id,
            ProductCode = entity.ProductCode,
            AvailableQuantity = entity.AvailableQuantity,
            LastUpdated = entity.LastUpdated
        };
    }
}
```

---

### **Step 6: Create Business Logic Service**

**File: `Application/Services/InventoryService.cs`**
```csharp
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Shared.Events;
using InventoryService.Application.Interfaces;

namespace InventoryService.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IInventoryRepository repository,
        ILogger<InventoryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<InventoryCheckResult> CheckAndReserveInventoryAsync(
        OrderPlacedEvent orderEvent, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking inventory for order {OrderId}", orderEvent.OrderId);

        var result = new InventoryCheckResult { IsSuccessful = true };
        var productCodes = orderEvent.Items.Select(i => i.ProductCode).ToList();

        // Fetch all inventory items at once
        var inventoryItems = await _repository.GetByProductCodesAsync(productCodes, cancellationToken);
        var inventoryDict = inventoryItems.ToDictionary(i => i.ProductCode);

        // Check inventory for each order item
        foreach (var orderItem in orderEvent.Items)
        {
            if (!inventoryDict.TryGetValue(orderItem.ProductCode, out var inventoryItem))
            {
                result.IsSuccessful = false;
                result.MissingItems.Add(orderItem.ProductCode);
                _logger.LogWarning("Product {ProductCode} not found in inventory", orderItem.ProductCode);
                continue;
            }

            if (inventoryItem.AvailableQuantity < orderItem.Quantity)
            {
                result.IsSuccessful = false;
                result.MissingItems.Add(orderItem.ProductCode);
                _logger.LogWarning("Product {ProductCode} - insufficient inventory (requested: {Requested}, available: {Available})",
                    orderItem.ProductCode, orderItem.Quantity, inventoryItem.AvailableQuantity);
            }
        }

        // If all checks passed, reserve inventory
        if (result.IsSuccessful)
        {
            foreach (var orderItem in orderEvent.Items)
            {
                var inventoryItem = inventoryDict[orderItem.ProductCode];
                inventoryItem.AvailableQuantity -= orderItem.Quantity;
                inventoryItem.LastUpdated = DateTime.UtcNow;

                await _repository.UpdateAsync(inventoryItem, cancellationToken);

                _logger.LogInformation("Reserved {Quantity} units of {ProductCode} (remaining: {Remaining})",
                    orderItem.Quantity, orderItem.ProductCode, inventoryItem.AvailableQuantity);
            }

            _logger.LogInformation("Order {OrderId} - inventory reserved successfully", orderEvent.OrderId);
        }
        else
        {
            _logger.LogWarning("Order {OrderId} - insufficient inventory for products: {Products}",
                orderEvent.OrderId, string.Join(", ", result.MissingItems));
        }

        return result;
    }
}
```

---

### **Step 7: Refactor Handler to be Thin**

**File: `Application/Handlers/OrderPlacedHandler.cs`**
```csharp
using OrderProcessingSystem.Shared.Events;
using OrderProcessingSystem.Shared.Messaging;
using InventoryService.Application.Interfaces;

namespace InventoryService.Application.Handlers;

public class OrderPlacedHandler
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OrderPlacedHandler> _logger;

    public OrderPlacedHandler(
        IServiceScopeFactory serviceScopeFactory,
        IMessagePublisher publisher,
        ILogger<OrderPlacedHandler> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(OrderPlacedEvent orderEvent)
    {
        _logger.LogInformation("Processing order {OrderId} for customer {CustomerEmail}",
            orderEvent.OrderId, orderEvent.CustomerEmail);

        // Create scope for scoped services (IInventoryService uses scoped repository)
        using var scope = _serviceScopeFactory.CreateScope();
        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        // Delegate business logic to service
        var result = await inventoryService.CheckAndReserveInventoryAsync(orderEvent);

        // Publish appropriate event based on result
        if (result.IsSuccessful)
        {
            var reservedEvent = new InventoryReservedEvent(
                OrderId: orderEvent.OrderId,
                Timestamp: DateTime.UtcNow
            );

            await _publisher.PublishAsync("inventory.reserved", reservedEvent);
            _logger.LogInformation("Published inventory.reserved event for order {OrderId}", orderEvent.OrderId);
        }
        else
        {
            var insufficientEvent = new InventoryInsufficientEvent(
                OrderId: orderEvent.OrderId,
                MissingItems: result.MissingItems,
                Timestamp: DateTime.UtcNow
            );

            await _publisher.PublishAsync("inventory.insufficient", insufficientEvent);
            _logger.LogWarning("Published inventory.insufficient event for order {OrderId}", orderEvent.OrderId);
        }
    }
}
```

---

### **Step 8: Update Program.cs DI Configuration**

**File: `Program.cs`**
```csharp
using Microsoft.EntityFrameworkCore;
using InventoryService.Infrastructure.Persistence;
using InventoryService.Application.Interfaces;
using InventoryService.Infrastructure.Persistence.Repositories;
using InventoryService.Application.Handlers;
using OrderProcessingSystem.Shared.Messaging;
using OrderProcessingSystem.Shared.Events;

var builder = WebApplication.CreateBuilder(args);

// Configure Database
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("InventoryDatabase")));

// Initialize RabbitMQ connection asynchronously
var rabbitConfig = builder.Configuration.GetSection("RabbitMQ");
var rabbitHost = rabbitConfig["Host"] ?? "localhost";
var rabbitExchangeName = rabbitConfig["ExchangeName"] ?? "orders";
var rabbitFactory = new RabbitMQ.Client.ConnectionFactory { HostName = rabbitHost };
var rabbitConnection = await rabbitFactory.CreateConnectionAsync();

// Register RabbitMQ connection and messaging
builder.Services.AddSingleton<RabbitMQ.Client.IConnection>(rabbitConnection);
builder.Services.AddSingleton<IMessagePublisher>(sp =>
{
    var connection = sp.GetRequiredService<RabbitMQ.Client.IConnection>();
    return new RabbitMqPublisher(connection, rabbitExchangeName);
});
builder.Services.AddSingleton<IMessageConsumer>(sp =>
{
    var connection = sp.GetRequiredService<RabbitMQ.Client.IConnection>();
    return new RabbitMqConsumer(connection, rabbitExchangeName);
});

// Register Application Services
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IInventoryService, InventoryService.Application.Services.InventoryService>();

// Register Handler
builder.Services.AddSingleton<OrderPlacedHandler>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "liveness" });
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("InventoryDatabase") ?? "", name: "postgres", tags: new[] { "readiness" })
    .AddRabbitMQ(
        factory: sp => sp.GetRequiredService<RabbitMQ.Client.IConnection>(),
        name: "rabbitmq",
        tags: new[] { "readiness" }
    );

var app = builder.Build();

// Map health check endpoints
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("liveness")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("readiness")
});

// Start consuming messages
var consumer = app.Services.GetRequiredService<IMessageConsumer>();
var handler = app.Services.GetRequiredService<OrderPlacedHandler>();

_ = Task.Run(async () =>
{
    await consumer.SubscribeAsync<OrderPlacedEvent>(
        queueName: "inventory-queue",
        routingKey: "order.placed",
        handler: handler.HandleAsync
    );
    app.Logger.LogInformation("Started listening for order.placed events on queue 'inventory-queue'");
});

app.Run();
```

---

### **Step 9: Update Migration Files**

Update namespace in existing migration files:

**File: `Migrations/20251115161108_InitialCreate.Designer.cs`**
- Change: `using InventoryService.Data;` → `using InventoryService.Infrastructure.Persistence;`

**File: `Migrations/InventoryDbContextModelSnapshot.cs`**
- Change: `using InventoryService.Data;` → `using InventoryService.Infrastructure.Persistence;`

---

### **Step 10: Build and Test**

```powershell
# Build the solution
dotnet build

# Should succeed with no errors
```

---

## ✅ Key Decisions Applied (Same as OrderService)

### **1. Domain Models: Mutable Classes** ✅
- `InventoryItem` is a class (not record)
- Can add business logic methods later
- Compatible with EF tracking

### **2. Separate Domain and EF Models** ✅
- Domain: `Application/Models/InventoryItem.cs`
- EF Entity: `Infrastructure/Persistence/Entities/InventoryItemEntity.cs`
- Repository handles mapping

### **3. Business Logic in Service** ✅
- `InventoryService` handles checking and reservation logic
- Handler is thin - just orchestrates and publishes events
- Service depends only on abstractions (`IInventoryRepository`)

### **4. Return Types: IReadOnlyList** ✅
- Repository returns `IReadOnlyList<InventoryItem>`
- Explicit materialization
- Immutable contract

### **5. No FluentValidation Needed** ✅
- This is a message consumer, not an API
- Input comes from RabbitMQ events (already validated by OrderService)
- Business validation in service layer

---

## 🎯 Benefits After Refactoring

✅ **Testability**: Mock `IInventoryRepository` for unit tests  
✅ **Separation of Concerns**: Handler, Service, Repository - each has single responsibility  
✅ **Clean Dependencies**: Service knows nothing about EF or DbContext  
✅ **Maintainability**: Business logic centralized in service  
✅ **Flexibility**: Easy to swap repository implementation  
✅ **Domain-Centric**: Core domain models independent of infrastructure  

---

## 📂 Final Folder Structure

```
InventoryService/
├── Application/
│   ├── Interfaces/
│   │   ├── IInventoryRepository.cs
│   │   └── IInventoryService.cs
│   ├── Models/
│   │   └── InventoryItem.cs              # Domain model (clean POCO)
│   ├── Services/
│   │   └── InventoryService.cs           # Business logic
│   └── Handlers/
│       └── OrderPlacedHandler.cs         # Thin handler
├── Infrastructure/
│   └── Persistence/
│       ├── InventoryDbContext.cs
│       ├── Entities/
│       │   └── InventoryItemEntity.cs    # EF model
│       ├── Configurations/
│       │   └── InventoryItemEntityConfiguration.cs
│       └── Repositories/
│           └── InventoryRepository.cs    # Repository implementation
├── Migrations/
├── Properties/
└── Program.cs
```

---

## 🚀 Implementation Checklist

- [ ] Create `Application/Interfaces/IInventoryRepository.cs`
- [ ] Create `Application/Interfaces/IInventoryService.cs`
- [ ] Move `InventoryItem` to `Application/Models/`
- [ ] Create `Infrastructure/Persistence/Entities/InventoryItemEntity.cs`
- [ ] Create `Infrastructure/Persistence/Configurations/InventoryItemEntityConfiguration.cs`
- [ ] Update `InventoryDbContext.cs` to use entities and configurations
- [ ] Create `Infrastructure/Persistence/Repositories/InventoryRepository.cs`
- [ ] Create `Application/Services/InventoryService.cs`
- [ ] Move handler to `Application/Handlers/` and make it thin
- [ ] Update `Program.cs` DI configuration
- [ ] Update migration file namespaces
- [ ] Build and verify: `dotnet build`
- [ ] Test message consumption with actual order events

---

**Follow the same principles and patterns as OrderService for consistency!** 🎉
