using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.Services;
using OrderProcessingSystem.Shared.Messaging;
using OrderProcessingSystem.Shared.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Database
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("InventoryDatabase")));

// Configure RabbitMQ Publisher
builder.Services.AddSingleton<IMessagePublisher>(sp =>
{
    var config = builder.Configuration.GetSection("RabbitMQ");
    var host = config["Host"] ?? "localhost";
    var exchangeName = config["ExchangeName"] ?? "orders";
    return new RabbitMqPublisher(host, exchangeName);
});

// Configure RabbitMQ Consumer
builder.Services.AddSingleton<IMessageConsumer>(sp =>
{
    var config = builder.Configuration.GetSection("RabbitMQ");
    var host = config["Host"] ?? "localhost";
    var exchangeName = config["ExchangeName"] ?? "orders";
    return new RabbitMqConsumer(host, exchangeName);
});

// Register handler
builder.Services.AddSingleton<OrderPlacedHandler>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

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
