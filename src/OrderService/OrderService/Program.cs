using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderProcessingSystem.Shared.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Database
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("OrderDatabase")));

// Configure RabbitMQ Publisher
builder.Services.AddSingleton<IMessagePublisher>(sp =>
{
    var config = builder.Configuration.GetSection("RabbitMQ");
    var host = config["Host"] ?? "localhost";
    var exchangeName = config["ExchangeName"] ?? "orders";
    return new RabbitMqPublisher(host, exchangeName);
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
