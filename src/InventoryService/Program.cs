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

// Register Handler as Scoped (depends on scoped services)
builder.Services.AddScoped<OrderPlacedHandler>();

// Add health checks
// Liveness: Fast check that the service is running (no dependencies checked)
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "liveness" });

// Readiness: Check if the service is ready to handle requests (dependencies checked)
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("InventoryDatabase") ?? "", name: "postgres", tags: new[] { "readiness" })
    .AddRabbitMQ(
        factory: sp => sp.GetRequiredService<RabbitMQ.Client.IConnection>(),
        name: "rabbitmq",
        tags: new[] { "readiness" }
    );

var app = builder.Build();

// Apply database migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    app.Logger.LogInformation("Applying database migrations...");
    context.Database.Migrate();
    app.Logger.LogInformation("Database migrations applied successfully");
}

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

// Subscribe to order.placed events
// Create a scope for each message to get a fresh scoped handler instance
_ = Task.Run(async () =>
{
    await consumer.SubscribeAsync<OrderPlacedEvent>(
        queueName: "inventory-queue",
        routingKey: "order.placed",
        handler: async (orderEvent) =>
        {
            using var scope = app.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<OrderPlacedHandler>();
            await handler.HandleAsync(orderEvent);
        }
    );
    app.Logger.LogInformation("Started listening for order.placed events on queue 'inventory-queue'");
});

app.Run();
