# Order Processing System - Implementation Plan

## Project Overview

A microservices-based order processing system demonstrating event-driven architecture with:
- 3 .NET Core Web API services (Order, Inventory, Notification)
- RabbitMQ for message routing
- PostgreSQL for persistence
- Redis for caching
- Docker Compose for infrastructure

---

## Stage 1: Project Structure & Infrastructure Setup

### Objective
Set up the solution structure and get all infrastructure running in Docker.

### Tasks

1. **Create Solution Structure**
   ```
   OrderProcessingSystem/
   ├── docker-compose.yml
   ├── OrderProcessingSystem.sln
   ├── src/
   │   ├── OrderService/
   │   ├── InventoryService/
   │   ├── NotificationService/
   │   └── Shared/
   │       └── OrderProcessingSystem.Shared/
   └── README.md
   ```

2. **Create docker-compose.yml**
   - RabbitMQ (with management plugin on port 15672)
   - PostgreSQL (port 5432)
   - Redis (port 6379)
   - Networks configuration

3. **Create Shared Class Library Project**
   - Project: `OrderProcessingSystem.Shared`
   - Add NuGet packages:
     - `RabbitMQ.Client` or `Azure.Messaging.ServiceBus`
     - `StackExchange.Redis`
   - Create folder structure:
     ```
     OrderProcessingSystem.Shared/
     ├── Events/
     ├── Messaging/
     └── Constants/
     ```

4. **Define Event Models (in Shared project)**
   ```csharp
   // Events/OrderPlacedEvent.cs
   public class OrderPlacedEvent
   {
       public Guid OrderId { get; set; }
       public string CustomerEmail { get; set; }
       public List<OrderItemDto> Items { get; set; }
       public DateTime Timestamp { get; set; }
   }

   // Events/InventoryReservedEvent.cs
   public class InventoryReservedEvent
   {
       public Guid OrderId { get; set; }
       public DateTime Timestamp { get; set; }
   }

   // Events/InventoryInsufficientEvent.cs
   public class InventoryInsufficientEvent
   {
       public Guid OrderId { get; set; }
       public List<string> MissingItems { get; set; }
       public DateTime Timestamp { get; set; }
   }

   // Events/OrderItemDto.cs
   public class OrderItemDto
   {
       public string ProductCode { get; set; }
       public int Quantity { get; set; }
   }
   ```

5. **Create Message Publisher Interface (in Shared)**
   ```csharp
   // Messaging/IMessagePublisher.cs
   public interface IMessagePublisher
   {
       Task PublishAsync<T>(string routingKey, T message) where T : class;
   }
   ```

6. **Verify Infrastructure**
   - Run `docker-compose up -d`
   - Access RabbitMQ Management UI: http://localhost:15672 (guest/guest)
   - Verify PostgreSQL connection
   - Verify Redis connection

### Acceptance Criteria
- ✅ Solution structure created
- ✅ Docker Compose file ready with all services
- ✅ All infrastructure containers running
- ✅ Shared library project created with event models
- ✅ Can access RabbitMQ management UI

### Deliverables
- Solution file with Shared project
- docker-compose.yml
- Event model classes
- Infrastructure is up and accessible

---

## Stage 2: Order Service - Basic API & Database

### Objective
Create Order Service with REST API, PostgreSQL database, and EF Core.

### Tasks

1. **Create Order Service Web API Project**
   ```bash
   dotnet new webapi -n OrderService
   ```

2. **Add NuGet Packages**
   - `Microsoft.EntityFrameworkCore`
   - `Microsoft.EntityFrameworkCore.Design`
   - `Npgsql.EntityFrameworkCore.PostgreSQL`
   - `Microsoft.EntityFrameworkCore.Tools`
   - Reference to `OrderProcessingSystem.Shared`

3. **Create Domain Models**
   ```csharp
   // Models/Order.cs
   public class Order
   {
       public Guid Id { get; set; }
       public string CustomerEmail { get; set; }
       public OrderStatus Status { get; set; }
       public DateTime CreatedAt { get; set; }
       public List<OrderItem> Items { get; set; }
   }

   // Models/OrderItem.cs
   public class OrderItem
   {
       public Guid Id { get; set; }
       public Guid OrderId { get; set; }
       public string ProductCode { get; set; }
       public int Quantity { get; set; }
   }

   // Models/OrderStatus.cs
   public enum OrderStatus
   {
       Pending,
       InventoryReserved,
       InventoryInsufficient,
       Failed
   }
   ```

4. **Create DbContext**
   ```csharp
   // Data/OrderDbContext.cs
   public class OrderDbContext : DbContext
   {
       public OrderDbContext(DbContextOptions<OrderDbContext> options) 
           : base(options) { }

       public DbSet<Order> Orders { get; set; }
       public DbSet<OrderItem> OrderItems { get; set; }

       protected override void OnModelCreating(ModelBuilder modelBuilder)
       {
           // Configure relationships, indexes, etc.
       }
   }
   ```

5. **Configure Database in appsettings.json**
   ```json
   {
     "ConnectionStrings": {
       "OrderDatabase": "Host=localhost;Port=5432;Database=orders_db;Username=postgres;Password=postgres"
     }
   }
   ```

6. **Register DbContext in Program.cs**
   ```csharp
   builder.Services.AddDbContext<OrderDbContext>(options =>
       options.UseNpgsql(builder.Configuration.GetConnectionString("OrderDatabase")));
   ```

7. **Create and Apply Migration**
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

8. **Create DTOs**
   ```csharp
   // DTOs/CreateOrderRequest.cs
   public class CreateOrderRequest
   {
       public string CustomerEmail { get; set; }
       public List<OrderItemDto> Items { get; set; }
   }

   // DTOs/OrderResponse.cs
   public class OrderResponse
   {
       public Guid Id { get; set; }
       public string CustomerEmail { get; set; }
       public OrderStatus Status { get; set; }
       public DateTime CreatedAt { get; set; }
       public List<OrderItemDto> Items { get; set; }
   }
   ```

9. **Create Orders Controller**
   ```csharp
   // Controllers/OrdersController.cs
   [ApiController]
   [Route("api/[controller]")]
   public class OrdersController : ControllerBase
   {
       private readonly OrderDbContext _context;

       [HttpPost]
       public async Task<ActionResult<OrderResponse>> CreateOrder(CreateOrderRequest request)
       {
           // Create order entity
           // Save to database
           // Return response
       }

       [HttpGet("{id}")]
       public async Task<ActionResult<OrderResponse>> GetOrder(Guid id)
       {
           // Fetch from database
           // Return response
       }
   }
   ```

### Acceptance Criteria
- ✅ Order Service API project created
- ✅ Database models and DbContext configured
- ✅ EF Core migrations created and applied
- ✅ Orders table created in PostgreSQL
- ✅ POST /api/orders endpoint works (creates order)
- ✅ GET /api/orders/{id} endpoint works (retrieves order)
- ✅ Order persisted in PostgreSQL

### Testing
```bash
# Create order
curl -X POST http://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "test@example.com",
    "items": [
      {"productCode": "PROD-001", "quantity": 2}
    ]
  }'

# Get order
curl http://localhost:5001/api/orders/{orderId}
```

---

## Stage 3: RabbitMQ Integration - Publisher

### Objective
Integrate RabbitMQ message publishing into Order Service.

### Tasks

1. **Add RabbitMQ NuGet Package to Order Service**
   - `RabbitMQ.Client`

2. **Implement RabbitMQ Publisher (in Shared project)**
   ```csharp
   // Messaging/RabbitMqPublisher.cs
   public class RabbitMqPublisher : IMessagePublisher
   {
       private readonly IConnection _connection;
       private readonly IModel _channel;
       private readonly string _exchangeName;

       public RabbitMqPublisher(string hostname, string exchangeName)
       {
           var factory = new ConnectionFactory { HostName = hostname };
           _connection = factory.CreateConnection();
           _channel = _connection.CreateModel();
           _exchangeName = exchangeName;

           // Declare exchange
           _channel.ExchangeDeclare(
               exchange: _exchangeName,
               type: ExchangeType.Topic,
               durable: true
           );
       }

       public Task PublishAsync<T>(string routingKey, T message) where T : class
       {
           var json = JsonSerializer.Serialize(message);
           var body = Encoding.UTF8.GetBytes(json);

           var properties = _channel.CreateBasicProperties();
           properties.Persistent = true;
           properties.MessageId = Guid.NewGuid().ToString();
           properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

           _channel.BasicPublish(
               exchange: _exchangeName,
               routingKey: routingKey,
               basicProperties: properties,
               body: body
           );

           return Task.CompletedTask;
       }
   }
   ```

3. **Configure RabbitMQ in Order Service appsettings.json**
   ```json
   {
     "RabbitMQ": {
       "Host": "localhost",
       "ExchangeName": "orders"
     }
   }
   ```

4. **Register Publisher in Order Service Program.cs**
   ```csharp
   builder.Services.AddSingleton<IMessagePublisher>(sp =>
   {
       var config = builder.Configuration.GetSection("RabbitMQ");
       return new RabbitMqPublisher(
           config["Host"],
           config["ExchangeName"]
       );
   });
   ```

5. **Update OrdersController to Publish Event**
   ```csharp
   [ApiController]
   [Route("api/[controller]")]
   public class OrdersController : ControllerBase
   {
       private readonly OrderDbContext _context;
       private readonly IMessagePublisher _publisher;

       public OrdersController(OrderDbContext context, IMessagePublisher publisher)
       {
           _context = context;
           _publisher = publisher;
       }

       [HttpPost]
       public async Task<ActionResult<OrderResponse>> CreateOrder(CreateOrderRequest request)
       {
           // Create and save order
           var order = new Order
           {
               Id = Guid.NewGuid(),
               CustomerEmail = request.CustomerEmail,
               Status = OrderStatus.Pending,
               CreatedAt = DateTime.UtcNow,
               Items = request.Items.Select(i => new OrderItem
               {
                   Id = Guid.NewGuid(),
                   ProductCode = i.ProductCode,
                   Quantity = i.Quantity
               }).ToList()
           };

           _context.Orders.Add(order);
           await _context.SaveChangesAsync();

           // Publish event
           var orderPlacedEvent = new OrderPlacedEvent
           {
               OrderId = order.Id,
               CustomerEmail = order.CustomerEmail,
               Items = order.Items.Select(i => new OrderItemDto
               {
                   ProductCode = i.ProductCode,
                   Quantity = i.Quantity
               }).ToList(),
               Timestamp = DateTime.UtcNow
           };

           await _publisher.PublishAsync("order.placed", orderPlacedEvent);

           // Return response
           return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, MapToResponse(order));
       }
   }
   ```

### Acceptance Criteria
- ✅ RabbitMQ publisher implemented
- ✅ Order Service publishes `OrderPlacedEvent` after creating order
- ✅ Exchange "orders" created in RabbitMQ
- ✅ Message visible in RabbitMQ Management UI
- ✅ Message has correct routing key: "order.placed"

### Testing
- Create an order via API
- Check RabbitMQ Management UI → Exchanges → "orders"
- Verify message was published with routing key "order.placed"

---

## Stage 4: Inventory Service - Consumer & Database

### Objective
Create Inventory Service that consumes messages and manages inventory.

### Tasks

1. **Create Inventory Service Web API Project**
   ```bash
   dotnet new webapi -n InventoryService
   ```

2. **Add NuGet Packages**
   - `Microsoft.EntityFrameworkCore`
   - `Microsoft.EntityFrameworkCore.Design`
   - `Npgsql.EntityFrameworkCore.PostgreSQL`
   - `RabbitMQ.Client`
   - Reference to `OrderProcessingSystem.Shared`

3. **Create Domain Models**
   ```csharp
   // Models/InventoryItem.cs
   public class InventoryItem
   {
       public Guid Id { get; set; }
       public string ProductCode { get; set; }
       public int AvailableQuantity { get; set; }
       public DateTime LastUpdated { get; set; }
   }
   ```

4. **Create DbContext**
   ```csharp
   // Data/InventoryDbContext.cs
   public class InventoryDbContext : DbContext
   {
       public InventoryDbContext(DbContextOptions<InventoryDbContext> options) 
           : base(options) { }

       public DbSet<InventoryItem> InventoryItems { get; set; }

       protected override void OnModelCreating(ModelBuilder modelBuilder)
       {
           modelBuilder.Entity<InventoryItem>()
               .HasIndex(i => i.ProductCode)
               .IsUnique();

           // Seed data
           modelBuilder.Entity<InventoryItem>().HasData(
               new InventoryItem { Id = Guid.NewGuid(), ProductCode = "PROD-001", AvailableQuantity = 100, LastUpdated = DateTime.UtcNow },
               new InventoryItem { Id = Guid.NewGuid(), ProductCode = "PROD-002", AvailableQuantity = 50, LastUpdated = DateTime.UtcNow },
               new InventoryItem { Id = Guid.NewGuid(), ProductCode = "PROD-003", AvailableQuantity = 0, LastUpdated = DateTime.UtcNow }
           );
       }
   }
   ```

5. **Configure Database and Apply Migration**
   ```json
   {
     "ConnectionStrings": {
       "InventoryDatabase": "Host=localhost;Port=5432;Database=inventory_db;Username=postgres;Password=postgres"
     }
   }
   ```

6. **Create Message Consumer Interface**
   ```csharp
   // Messaging/IMessageConsumer.cs (in Shared)
   public interface IMessageConsumer
   {
       void Subscribe<T>(string queueName, string routingKey, Func<T, Task> handler) where T : class;
   }
   ```

7. **Implement RabbitMQ Consumer**
   ```csharp
   // Messaging/RabbitMqConsumer.cs (in Shared)
   public class RabbitMqConsumer : IMessageConsumer
   {
       private readonly IConnection _connection;
       private readonly IModel _channel;
       private readonly string _exchangeName;

       public RabbitMqConsumer(string hostname, string exchangeName)
       {
           var factory = new ConnectionFactory { HostName = hostname };
           _connection = factory.CreateConnection();
           _channel = _connection.CreateModel();
           _exchangeName = exchangeName;
       }

       public void Subscribe<T>(string queueName, string routingKey, Func<T, Task> handler) where T : class
       {
           // Declare queue
           _channel.QueueDeclare(
               queue: queueName,
               durable: true,
               exclusive: false,
               autoDelete: false
           );

           // Bind queue to exchange
           _channel.QueueBind(
               queue: queueName,
               exchange: _exchangeName,
               routingKey: routingKey
           );

           var consumer = new EventingBasicConsumer(_channel);
           consumer.Received += async (model, ea) =>
           {
               try
               {
                   var body = ea.Body.ToArray();
                   var json = Encoding.UTF8.GetString(body);
                   var message = JsonSerializer.Deserialize<T>(json);

                   await handler(message);

                   _channel.BasicAck(ea.DeliveryTag, false);
               }
               catch (Exception ex)
               {
                   // Log error
                   _channel.BasicNack(ea.DeliveryTag, false, false); // Send to DLQ
               }
           };

           _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
       }
   }
   ```

8. **Create Inventory Service Handler**
   ```csharp
   // Services/OrderPlacedHandler.cs
   public class OrderPlacedHandler
   {
       private readonly InventoryDbContext _context;
       private readonly IMessagePublisher _publisher;
       private readonly ILogger<OrderPlacedHandler> _logger;

       public OrderPlacedHandler(
           InventoryDbContext context,
           IMessagePublisher publisher,
           ILogger<OrderPlacedHandler> logger)
       {
           _context = context;
           _publisher = publisher;
           _logger = logger;
       }

       public async Task HandleAsync(OrderPlacedEvent orderEvent)
       {
           _logger.LogInformation($"Processing order {orderEvent.OrderId}");

           var missingItems = new List<string>();

           foreach (var item in orderEvent.Items)
           {
               var inventoryItem = await _context.InventoryItems
                   .FirstOrDefaultAsync(i => i.ProductCode == item.ProductCode);

               if (inventoryItem == null || inventoryItem.AvailableQuantity < item.Quantity)
               {
                   missingItems.Add(item.ProductCode);
               }
           }

           if (missingItems.Any())
           {
               // Insufficient inventory
               var insufficientEvent = new InventoryInsufficientEvent
               {
                   OrderId = orderEvent.OrderId,
                   MissingItems = missingItems,
                   Timestamp = DateTime.UtcNow
               };

               await _publisher.PublishAsync("inventory.insufficient", insufficientEvent);
               _logger.LogWarning($"Order {orderEvent.OrderId} - insufficient inventory");
           }
           else
           {
               // Reserve inventory
               foreach (var item in orderEvent.Items)
               {
                   var inventoryItem = await _context.InventoryItems
                       .FirstAsync(i => i.ProductCode == item.ProductCode);

                   inventoryItem.AvailableQuantity -= item.Quantity;
                   inventoryItem.LastUpdated = DateTime.UtcNow;
               }

               await _context.SaveChangesAsync();

               var reservedEvent = new InventoryReservedEvent
               {
                   OrderId = orderEvent.OrderId,
                   Timestamp = DateTime.UtcNow
               };

               await _publisher.PublishAsync("inventory.reserved", reservedEvent);
               _logger.LogInformation($"Order {orderEvent.OrderId} - inventory reserved");
           }
       }
   }
   ```

9. **Register Services and Start Consumer**
   ```csharp
   // Program.cs
   builder.Services.AddDbContext<InventoryDbContext>(options =>
       options.UseNpgsql(builder.Configuration.GetConnectionString("InventoryDatabase")));

   builder.Services.AddSingleton<IMessagePublisher>(sp =>
   {
       var config = builder.Configuration.GetSection("RabbitMQ");
       return new RabbitMqPublisher(config["Host"], config["ExchangeName"]);
   });

   builder.Services.AddSingleton<IMessageConsumer>(sp =>
   {
       var config = builder.Configuration.GetSection("RabbitMQ");
       return new RabbitMqConsumer(config["Host"], config["ExchangeName"]);
   });

   builder.Services.AddScoped<OrderPlacedHandler>();

   var app = builder.Build();

   // Start consuming messages
   var consumer = app.Services.GetRequiredService<IMessageConsumer>();
   using (var scope = app.Services.CreateScope())
   {
       var handler = scope.ServiceProvider.GetRequiredService<OrderPlacedHandler>();
       consumer.Subscribe<OrderPlacedEvent>("inventory-queue", "order.placed", handler.HandleAsync);
   }

   app.Run();
   ```

### Acceptance Criteria
- ✅ Inventory Service created with database
- ✅ Inventory items seeded in database
- ✅ Service subscribes to "order.placed" messages
- ✅ Service checks inventory availability
- ✅ Service publishes "inventory.reserved" when sufficient stock
- ✅ Service publishes "inventory.insufficient" when out of stock
- ✅ Inventory quantities updated in database

### Testing
- Create order with PROD-001 (should succeed - inventory available)
- Check RabbitMQ: "inventory.reserved" message published
- Create order with PROD-003 (should fail - out of stock)
- Check RabbitMQ: "inventory.insufficient" message published
- Verify inventory quantities updated in database

---

## Stage 5: Notification Service - Consumer with Dead Letter Queue

### Objective
Create Notification Service with DLQ handling for failed messages.

### Tasks

1. **Create Notification Service Web API Project**
   ```bash
   dotnet new webapi -n NotificationService
   ```

2. **Add NuGet Packages**
   - `RabbitMQ.Client`
   - Reference to `OrderProcessingSystem.Shared`

3. **Configure DLQ in RabbitMQ Consumer**
   Update `RabbitMqConsumer` to support DLQ:
   ```csharp
   public void Subscribe<T>(string queueName, string routingKey, Func<T, Task> handler, int maxRetries = 3) where T : class
   {
       // Declare DLQ
       var dlqName = $"{queueName}.dlq";
       _channel.QueueDeclare(
           queue: dlqName,
           durable: true,
           exclusive: false,
           autoDelete: false
       );

       // Declare main queue with DLQ arguments
       var args = new Dictionary<string, object>
       {
           { "x-dead-letter-exchange", "" },
           { "x-dead-letter-routing-key", dlqName }
       };

       _channel.QueueDeclare(
           queue: queueName,
           durable: true,
           exclusive: false,
           autoDelete: false,
           arguments: args
       );

       _channel.QueueBind(queue: queueName, exchange: _exchangeName, routingKey: routingKey);

       var consumer = new EventingBasicConsumer(_channel);
       consumer.Received += async (model, ea) =>
       {
           var retryCount = 0;
           if (ea.BasicProperties.Headers != null && ea.BasicProperties.Headers.ContainsKey("x-retry-count"))
           {
               retryCount = Convert.ToInt32(ea.BasicProperties.Headers["x-retry-count"]);
           }

           try
           {
               var body = ea.Body.ToArray();
               var json = Encoding.UTF8.GetString(body);
               var message = JsonSerializer.Deserialize<T>(json);

               await handler(message);
               _channel.BasicAck(ea.DeliveryTag, false);
           }
           catch (Exception ex)
           {
               if (retryCount < maxRetries)
               {
                   // Retry with incremented counter
                   var properties = _channel.CreateBasicProperties();
                   properties.Headers = new Dictionary<string, object>
                   {
                       { "x-retry-count", retryCount + 1 }
                   };
                   
                   _channel.BasicPublish("", queueName, properties, ea.Body);
                   _channel.BasicAck(ea.DeliveryTag, false);
               }
               else
               {
                   // Max retries exceeded - reject to DLQ
                   _channel.BasicNack(ea.DeliveryTag, false, false);
               }
           }
       };

       _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
   }
   ```

4. **Create Notification Handlers**
   ```csharp
   // Services/InventoryReservedHandler.cs
   public class InventoryReservedHandler
   {
       private readonly ILogger<InventoryReservedHandler> _logger;

       public InventoryReservedHandler(ILogger<InventoryReservedHandler> logger)
       {
           _logger = logger;
       }

       public Task HandleAsync(InventoryReservedEvent notification)
       {
           _logger.LogInformation($"✅ Order {notification.OrderId} - Inventory reserved successfully!");
           // Simulate sending email/SMS
           return Task.CompletedTask;
       }
   }

   // Services/InventoryInsufficientHandler.cs
   public class InventoryInsufficientHandler
   {
       private readonly ILogger<InventoryInsufficientHandler> _logger;
       private static int _callCount = 0;

       public InventoryInsufficientHandler(ILogger<InventoryInsufficientHandler> logger)
       {
           _logger = logger;
       }

       public Task HandleAsync(InventoryInsufficientEvent notification)
       {
           _callCount++;
           
           // Simulate failure for testing DLQ (fail first 3 times)
           if (_callCount <= 3)
           {
               _logger.LogError($"❌ Failed to process notification for order {notification.OrderId} (attempt {_callCount})");
               throw new Exception("Simulated notification failure");
           }

           _logger.LogWarning($"⚠️ Order {notification.OrderId} - Insufficient inventory for: {string.Join(", ", notification.MissingItems)}");
           return Task.CompletedTask;
       }
   }
   ```

5. **Register Services and Start Consumers**
   ```csharp
   // Program.cs
   builder.Services.AddSingleton<IMessageConsumer>(sp =>
   {
       var config = builder.Configuration.GetSection("RabbitMQ");
       return new RabbitMqConsumer(config["Host"], config["ExchangeName"]);
   });

   builder.Services.AddScoped<InventoryReservedHandler>();
   builder.Services.AddScoped<InventoryInsufficientHandler>();

   var app = builder.Build();

   var consumer = app.Services.GetRequiredService<IMessageConsumer>();

   // Subscribe to inventory.reserved
   using (var scope = app.Services.CreateScope())
   {
       var handler = scope.ServiceProvider.GetRequiredService<InventoryReservedHandler>();
       consumer.Subscribe<InventoryReservedEvent>("notification-reserved-queue", "inventory.reserved", handler.HandleAsync);
   }

   // Subscribe to inventory.insufficient (with DLQ)
   using (var scope = app.Services.CreateScope())
   {
       var handler = scope.ServiceProvider.GetRequiredService<InventoryInsufficientHandler>();
       consumer.Subscribe<InventoryInsufficientEvent>("notification-insufficient-queue", "inventory.insufficient", handler.HandleAsync, maxRetries: 3);
   }

   app.Run();
   ```

### Acceptance Criteria
- ✅ Notification Service created
- ✅ Subscribes to both "inventory.reserved" and "inventory.insufficient"
- ✅ Successfully processes "inventory.reserved" messages
- ✅ Simulates failure for "inventory.insufficient" messages
- ✅ Failed messages retry 3 times
- ✅ After max retries, messages move to DLQ
- ✅ DLQ visible in RabbitMQ Management UI

### Testing
- Create successful order → Check logs for "Inventory reserved successfully"
- Create failed order (out of stock) → Check logs for retry attempts
- Verify DLQ in RabbitMQ has the failed message after 3 retries

---

## Stage 6: Redis Integration - Caching

### Objective
Add Redis caching to Order Service and Inventory Service.

### Tasks

1. **Add Redis Package to Both Services**
   - `StackExchange.Redis`

2. **Create Redis Service (in Shared)**
   ```csharp
   // Caching/IRedisCacheService.cs
   public interface IRedisCacheService
   {
       Task<T?> GetAsync<T>(string key) where T : class;
       Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
       Task RemoveAsync(string key);
   }

   // Caching/RedisCacheService.cs
   public class RedisCacheService : IRedisCacheService
   {
       private readonly IConnectionMultiplexer _redis;
       private readonly IDatabase _db;

       public RedisCacheService(string connectionString)
       {
           _redis = ConnectionMultiplexer.Connect(connectionString);
           _db = _redis.GetDatabase();
       }

       public async Task<T?> GetAsync<T>(string key) where T : class
       {
           var value = await _db.StringGetAsync(key);
           if (value.IsNullOrEmpty)
               return null;

           return JsonSerializer.Deserialize<T>(value);
       }

       public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
       {
           var json = JsonSerializer.Serialize(value);
           await _db.StringSetAsync(key, json, expiration);
       }

       public async Task RemoveAsync(string key)
       {
           await _db.KeyDeleteAsync(key);
       }
   }
   ```

3. **Configure Redis in Order Service**
   ```json
   {
     "Redis": {
       "ConnectionString": "localhost:6379"
     }
   }
   ```

   ```csharp
   // Program.cs
   builder.Services.AddSingleton<IRedisCacheService>(sp =>
   {
       var connectionString = builder.Configuration["Redis:ConnectionString"];
       return new RedisCacheService(connectionString);
   });
   ```

4. **Add Caching to Order Service**
   ```csharp
   // Controllers/OrdersController.cs
   [HttpGet("{id}")]
   public async Task<ActionResult<OrderResponse>> GetOrder(Guid id)
   {
       // Try cache first
       var cacheKey = $"order:{id}";
       var cachedOrder = await _cache.GetAsync<OrderResponse>(cacheKey);
       if (cachedOrder != null)
       {
           _logger.LogInformation($"Order {id} retrieved from cache");
           return cachedOrder;
       }

       // Fetch from database
       var order = await _context.Orders
           .Include(o => o.Items)
           .FirstOrDefaultAsync(o => o.Id == id);

       if (order == null)
           return NotFound();

       var response = MapToResponse(order);

       // Cache for 5 minutes
       await _cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));
       _logger.LogInformation($"Order {id} retrieved from database and cached");

       return response;
   }
   ```

5. **Add Caching to Inventory Service**
   ```csharp
   // Services/OrderPlacedHandler.cs
   public async Task HandleAsync(OrderPlacedEvent orderEvent)
   {
       // Check cache first for hot items
       var missingItems = new List<string>();

       foreach (var item in orderEvent.Items)
       {
           var cacheKey = $"inventory:{item.ProductCode}";
           var cachedInventory = await _cache.GetAsync<InventoryItem>(cacheKey);

           InventoryItem inventoryItem;
           if (cachedInventory != null)
           {
               inventoryItem = cachedInventory;
               _logger.LogInformation($"Inventory for {item.ProductCode} retrieved from cache");
           }
           else
           {
               inventoryItem = await _context.InventoryItems
                   .FirstOrDefaultAsync(i => i.ProductCode == item.ProductCode);

               if (inventoryItem != null)
               {
                   await _cache.SetAsync(cacheKey, inventoryItem, TimeSpan.FromMinutes(2));
                   _logger.LogInformation($"Inventory for {item.ProductCode} cached");
               }
           }

           if (inventoryItem == null || inventoryItem.AvailableQuantity < item.Quantity)
           {
               missingItems.Add(item.ProductCode);
           }
       }

       // Process as before...

       // Invalidate cache after updating inventory
       if (!missingItems.Any())
       {
           foreach (var item in orderEvent.Items)
           {
               await _cache.RemoveAsync($"inventory:{item.ProductCode}");
           }
       }
   }
   ```

### Acceptance Criteria
- ✅ Redis service implemented
- ✅ Order Service caches order details
- ✅ Inventory Service caches hot inventory items
- ✅ Cache hit logs visible
- ✅ Cache invalidation works after inventory update
- ✅ Can verify cache entries in Redis CLI or Redis Insight

### Testing
```bash
# Get order twice - second time should be from cache
curl http://localhost:5001/api/orders/{orderId}
curl http://localhost:5001/api/orders/{orderId}

# Check Redis
docker exec -it <redis-container> redis-cli
> KEYS *
> GET order:{orderId}
```

---

## Stage 7: Dockerization - All Services in Docker Compose

### Objective
Containerize all services and run entire system with `docker-compose up`.

### Tasks

1. **Create Dockerfile for Order Service**
   ```dockerfile
   # src/OrderService/Dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
   WORKDIR /app
   EXPOSE 80

   FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
   WORKDIR /src
   COPY ["OrderService/OrderService.csproj", "OrderService/"]
   COPY ["Shared/OrderProcessingSystem.Shared/OrderProcessingSystem.Shared.csproj", "Shared/OrderProcessingSystem.Shared/"]
   RUN dotnet restore "OrderService/OrderService.csproj"
   COPY . .
   WORKDIR "/src/OrderService"
   RUN dotnet build "OrderService.csproj" -c Release -o /app/build

   FROM build AS publish
   RUN dotnet publish "OrderService.csproj" -c Release -o /app/publish

   FROM base AS final
   WORKDIR /app
   COPY --from=publish /app/publish .
   ENTRYPOINT ["dotnet", "OrderService.dll"]
   ```

2. **Create Dockerfiles for Inventory and Notification Services**
   (Similar structure, adjust project names)

3. **Update docker-compose.yml**
   ```yaml
   version: '3.8'

   services:
     postgres:
       image: postgres:15
       environment:
         POSTGRES_USER: postgres
         POSTGRES_PASSWORD: postgres
       ports:
         - "5432:5432"
       volumes:
         - postgres_data:/var/lib/postgresql/data
       healthcheck:
         test: ["CMD-SHELL", "pg_isready -U postgres"]
         interval: 10s
         timeout: 5s
         retries: 5

     rabbitmq:
       image: rabbitmq:3-management
       environment:
         RABBITMQ_DEFAULT_USER: guest
         RABBITMQ_DEFAULT_PASS: guest
       ports:
         - "5672:5672"
         - "15672:15672"
       healthcheck:
         test: ["CMD", "rabbitmq-diagnostics", "ping"]
         interval: 10s
         timeout: 5s
         retries: 5

     redis:
       image: redis:7-alpine
       ports:
         - "6379:6379"
       healthcheck:
         test: ["CMD", "redis-cli", "ping"]
         interval: 10s
         timeout: 5s
         retries: 5

     order-service:
       build:
         context: ./src
         dockerfile: OrderService/Dockerfile
       environment:
         - ASPNETCORE_ENVIRONMENT=Development
         - ConnectionStrings__OrderDatabase=Host=postgres;Port=5432;Database=orders_db;Username=postgres;Password=postgres
         - RabbitMQ__Host=rabbitmq
         - RabbitMQ__ExchangeName=orders
         - Redis__ConnectionString=redis:6379
       ports:
         - "5001:80"
       depends_on:
         postgres:
           condition: service_healthy
         rabbitmq:
           condition: service_healthy
         redis:
           condition: service_healthy

     inventory-service:
       build:
         context: ./src
         dockerfile: InventoryService/Dockerfile
       environment:
         - ASPNETCORE_ENVIRONMENT=Development
         - ConnectionStrings__InventoryDatabase=Host=postgres;Port=5432;Database=inventory_db;Username=postgres;Password=postgres
         - RabbitMQ__Host=rabbitmq
         - RabbitMQ__ExchangeName=orders
         - Redis__ConnectionString=redis:6379
       depends_on:
         postgres:
           condition: service_healthy
         rabbitmq:
           condition: service_healthy
         redis:
           condition: service_healthy

     notification-service:
       build:
         context: ./src
         dockerfile: NotificationService/Dockerfile
       environment:
         - ASPNETCORE_ENVIRONMENT=Development
         - RabbitMQ__Host=rabbitmq
         - RabbitMQ__ExchangeName=orders
       depends_on:
         rabbitmq:
           condition: service_healthy

   volumes:
     postgres_data:
   ```

4. **Add Database Initialization**
   Create migration script or use EF migrations in Dockerfile:
   ```dockerfile
   # Add to Order Service Dockerfile before ENTRYPOINT
   RUN dotnet tool install --global dotnet-ef
   ENV PATH="$PATH:/root/.dotnet/tools"
   ```

   Or create a startup migration service:
   ```csharp
   // Program.cs (both Order and Inventory services)
   using (var scope = app.Services.CreateScope())
   {
       var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
       db.Database.Migrate(); // Apply migrations automatically
   }
   ```

5. **Update Connection Strings in appsettings.json**
   Make them environment-variable friendly:
   ```json
   {
     "ConnectionStrings": {
       "OrderDatabase": "Host=localhost;Port=5432;Database=orders_db;Username=postgres;Password=postgres"
     },
     "RabbitMQ": {
       "Host": "localhost",
       "ExchangeName": "orders"
     },
     "Redis": {
       "ConnectionString": "localhost:6379"
     }
   }
   ```

### Acceptance Criteria
- ✅ All services have Dockerfiles
- ✅ docker-compose.yml includes all services and infrastructure
- ✅ `docker-compose up -d` starts entire system
- ✅ All services healthy and running
- ✅ Databases auto-migrated on startup
- ✅ End-to-end flow works in Docker environment

### Testing
```bash
# Start everything
docker-compose up -d

# Check services
docker-compose ps

# Test end-to-end
curl -X POST http://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "test@example.com",
    "items": [{"productCode": "PROD-001", "quantity": 2}]
  }'

# Check logs
docker-compose logs order-service
docker-compose logs inventory-service
docker-compose logs notification-service

# Check RabbitMQ
# Open browser: http://localhost:15672 (guest/guest)

# Cleanup
docker-compose down -v
```

---

## Stage 8: Testing & Documentation

### Objective
Add comprehensive testing scenarios and documentation.

### Tasks

1. **Create Test Scenarios Document**
   - Happy path (successful order)
   - Insufficient inventory path
   - Cache hit/miss scenarios
   - DLQ verification

2. **Create Postman Collection or HTTP file**
   ```http
   ### Create Order - Success Case
   POST http://localhost:5001/api/orders
   Content-Type: application/json

   {
     "customerEmail": "success@example.com",
     "items": [
       {"productCode": "PROD-001", "quantity": 2}
     ]
   }

   ### Create Order - Insufficient Inventory
   POST http://localhost:5001/api/orders
   Content-Type: application/json

   {
     "customerEmail": "fail@example.com",
     "items": [
       {"productCode": "PROD-003", "quantity": 1}
     ]
   }

   ### Get Order
   GET http://localhost:5001/api/orders/{{orderId}}
   ```

3. **Update README.md**
   Include:
   - Project overview
   - Architecture diagram (ASCII or link to image)
   - Technologies used
   - Quick start guide
   - Testing instructions
   - RabbitMQ routing explanation
   - Redis caching strategy

4. **Add Observability (Optional Enhancement)**
   - Add structured logging (Serilog)
   - Add health checks endpoint
   - Add Swagger/OpenAPI

5. **Create Architecture Diagram**
   ```
   ┌─────────────────────────────────────────────────────────────────┐
   │                         Order Service                           │
   │  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐     │
   │  │ REST API     │───▶│ PostgreSQL   │    │ Redis Cache  │     │
   │  │ (Port 5001)  │    │ (orders_db)  │    │              │     │
   │  └──────────────┘    └──────────────┘    └──────────────┘     │
   │         │                                                        │
   │         │ Publishes: order.placed                               │
   │         ▼                                                        │
   └─────────────────────────────────────────────────────────────────┘
                              │
                              │
                 ┌────────────▼────────────┐
                 │      RabbitMQ           │
                 │   Exchange: orders      │
                 │   Type: Topic           │
                 └────────────┬────────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
    order.placed    inventory.reserved  inventory.insufficient
              │               │               │
   ┌──────────▼─────────────┐│               │
   │  Inventory Service     ││               │
   │ ┌──────────────┐       ││               │
   │ │ PostgreSQL   │       ││               │
   │ │(inventory_db)│       ││               │
   │ └──────────────┘       ││               │
   │ ┌──────────────┐       ││               │
   │ │ Redis Cache  │       ││               │
   │ └──────────────┘       ││               │
   └────────────────────────┘│               │
                              │               │
              ┌───────────────┴───────────────┘
              │
              ▼
   ┌─────────────────────────┐
   │  Notification Service   │
   │  ┌──────────────────┐   │
   │  │ Dead Letter      │   │
   │  │ Queue (DLQ)      │   │
   │  │ Max Retries: 3   │   │
   │  └──────────────────┘   │
   └─────────────────────────┘
   ```

### Acceptance Criteria
- ✅ Test scenarios documented
- ✅ README.md complete with setup instructions
- ✅ HTTP/Postman collection created
- ✅ Architecture diagram included
- ✅ All features verified working end-to-end

---

## Verification Checklist

After completing all stages, verify:

- [ ] All infrastructure runs with `docker-compose up -d`
- [ ] Order Service API accessible at http://localhost:5001
- [ ] PostgreSQL has two databases: orders_db, inventory_db
- [ ] RabbitMQ Management UI accessible at http://localhost:15672
- [ ] Redis accessible and caching works
- [ ] Create order → Order saved to PostgreSQL
- [ ] Create order → Message published to RabbitMQ
- [ ] Inventory Service consumes message and checks stock
- [ ] Sufficient inventory → Updates PostgreSQL and publishes success event
- [ ] Insufficient inventory → Publishes failure event
- [ ] Notification Service logs success notifications
- [ ] Notification Service retries failures 3 times
- [ ] Failed messages moved to DLQ after max retries
- [ ] Redis caches order and inventory data
- [ ] Cache invalidation works correctly
- [ ] All services log meaningful information

---

## Common Issues & Solutions

### Issue: Database migration fails on startup
**Solution**: Ensure health checks pass before services start. Add retry logic or manual migration step.

### Issue: RabbitMQ connection refused
**Solution**: Check that RabbitMQ is healthy before services connect. Use connection retry logic.

### Issue: Messages not routing correctly
**Solution**: Verify exchange type (Topic), queue bindings, and routing keys match exactly.

### Issue: DLQ not working
**Solution**: Ensure queue declared with x-dead-letter-exchange and x-dead-letter-routing-key arguments.

### Issue: Redis cache not working
**Solution**: Check Redis connection string, ensure serialization/deserialization works correctly.

### Issue: Docker build fails
**Solution**: Verify all project references are correct, check Dockerfile COPY paths.

---

## Next Steps & Enhancements

After completing the basic implementation, consider adding:

1. **API Gateway** (Ocelot or YARP)
2. **Authentication/Authorization** (JWT)
3. **Distributed Tracing** (OpenTelemetry + Jaeger)
4. **Monitoring** (Prometheus + Grafana)
5. **Idempotency** handling with message deduplication
6. **Saga Pattern** for distributed transactions
7. **Circuit Breaker** (Polly)
8. **Rate Limiting**
9. **Integration Tests** with TestContainers
10. **Kubernetes Deployment** (Helm charts)

---

## Resources

- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [Docker Compose](https://docs.docker.com/compose/)
- [.NET Microservices Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/)

---

**Good luck with your implementation! 🚀**
