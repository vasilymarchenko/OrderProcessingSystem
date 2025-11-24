using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderProcessingSystem.Shared.Messaging;
using OrderService.Infrastructure.Persistence;
using OrderService.Models;

namespace OrderService.Application.Services;

public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly int _maxRetryCount = 5;

    public OutboxPublisherService(
        IServiceProvider serviceProvider,
        ILogger<OutboxPublisherService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxPublisherService stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        // Get pending or failed messages that are ready for retry
        var messages = await dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending ||
                       (m.Status == OutboxMessageStatus.Failed &&
                        m.RetryCount < _maxRetryCount &&
                        (m.NextRetryAt == null || m.NextRetryAt <= DateTime.UtcNow)))
            .OrderBy(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (!messages.Any())
        {
            return;
        }

        _logger.LogInformation("Processing {Count} outbox messages", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                var result = await publisher.PublishAsync(message.RoutingKey, message.Payload);

                if (result == PublishResult.Success)
                {
                    message.Status = OutboxMessageStatus.Published;
                    message.PublishedAt = DateTime.UtcNow;
                    message.LastError = null;
                    
                    _logger.LogInformation(
                        "Published outbox message {MessageId} with routing key {RoutingKey}",
                        message.Id, message.RoutingKey);
                }
                else
                {
                    message.Status = OutboxMessageStatus.Failed;
                    message.RetryCount++;
                    message.LastError = "Broker error";
                    message.NextRetryAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, message.RetryCount)); // Exponential backoff

                    _logger.LogWarning(
                        "Failed to publish outbox message {MessageId}: {Error}. Retry {RetryCount}/{MaxRetries}",
                        message.Id, message.LastError, message.RetryCount, _maxRetryCount);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing outbox message {MessageId}", message.Id);
                
                message.Status = OutboxMessageStatus.Failed;
                message.RetryCount++;
                message.LastError = ex.Message;
                message.NextRetryAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, message.RetryCount));
                
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
