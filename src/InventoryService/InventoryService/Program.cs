using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.Services;
using OrderProcessingSystem.Shared.Messaging;
using OrderProcessingSystem.Shared.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Note: No controllers needed - this is a message consumer service

// Configure Database
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("InventoryDatabase")));

// Initialize RabbitMQ connection asynchronously before service registration
var rabbitConfig = builder.Configuration.GetSection("RabbitMQ");
var rabbitHost = rabbitConfig["Host"] ?? "localhost";
var rabbitExchangeName = rabbitConfig["ExchangeName"] ?? "orders";
var rabbitFactory = new RabbitMQ.Client.ConnectionFactory { HostName = rabbitHost };
var rabbitConnection = await rabbitFactory.CreateConnectionAsync();

// Register the pre-created RabbitMQ connection as singleton
builder.Services.AddSingleton<RabbitMQ.Client.IConnection>(rabbitConnection);

// Configure RabbitMQ Publisher (uses shared connection)
builder.Services.AddSingleton<IMessagePublisher>(sp =>
{
    var connection = sp.GetRequiredService<RabbitMQ.Client.IConnection>();
    return new RabbitMqPublisher(connection, rabbitExchangeName);
});

// Configure RabbitMQ Consumer (uses shared connection)
builder.Services.AddSingleton<IMessageConsumer>(sp =>
{
    var connection = sp.GetRequiredService<RabbitMQ.Client.IConnection>();
    return new RabbitMqConsumer(connection, rabbitExchangeName);
});

// Register handler
builder.Services.AddSingleton<OrderPlacedHandler>();

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


// No need for OpenAPI/Swagger - this is a message consumer service

var app = builder.Build();

// Configure the HTTP request pipeline.
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

// Subscribe to order.placed events
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
