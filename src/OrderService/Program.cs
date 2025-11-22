using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Persistence;
using OrderService.Application.Interfaces;
using OrderService.Infrastructure.Persistence.Repositories;
using OrderProcessingSystem.Shared.Messaging;
using FluentValidation;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Configure Database
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("OrderDatabase")));

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

// Register Application Services
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService.Application.Services.OrderService>();

// Add health checks
// Liveness: Fast check that the service is running (no dependencies checked)
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "liveness" });

// Readiness: Check if the service is ready to handle requests (dependencies checked)
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("OrderDatabase") ?? "", name: "postgres", tags: new[] { "readiness" })
    .AddRabbitMQ(
        factory: sp => sp.GetRequiredService<RabbitMQ.Client.IConnection>(),
        name: "rabbitmq",
        tags: new[] { "readiness" }
    );

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply database migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    app.Logger.LogInformation("Applying database migrations...");
    context.Database.Migrate();
    app.Logger.LogInformation("Database migrations applied successfully");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
