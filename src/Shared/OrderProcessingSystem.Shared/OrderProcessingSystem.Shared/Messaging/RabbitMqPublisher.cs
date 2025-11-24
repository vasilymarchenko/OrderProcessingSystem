using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;

namespace OrderProcessingSystem.Shared.Messaging;

public class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly IChannel _channel;
    private readonly string _exchangeName;
    private readonly ILogger<RabbitMqPublisher>? _logger;
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<bool>> _pendingConfirms = new();
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    
    public RabbitMqPublisher(IChannel channel, string exchangeName, ILogger<RabbitMqPublisher>? logger = null)
    {
        _channel = channel;
        _exchangeName = exchangeName;
        _logger = logger;

        _channel.BasicAcksAsync += async (sender, args) =>
        {
            if (args.Multiple)
            {
                var confirmed = _pendingConfirms.Keys.Where(k => k <= args.DeliveryTag).ToList();
                foreach (var seq in confirmed)
                {
                    if (_pendingConfirms.TryRemove(seq, out var tcs))
                        tcs.TrySetResult(true);
                }
            }
            else
            {
                if (_pendingConfirms.TryRemove(args.DeliveryTag, out var tcs))
                    tcs.TrySetResult(true);
            }
            await Task.CompletedTask;
        };

        _channel.BasicNacksAsync += async (sender, args) =>
        {
            if (args.Multiple)
            {
                var nacked = _pendingConfirms.Keys.Where(k => k <= args.DeliveryTag).ToList();
                foreach (var seq in nacked)
                {
                    if (_pendingConfirms.TryRemove(seq, out var tcs))
                        tcs.TrySetResult(false);
                }
            }
            else
            {
                if (_pendingConfirms.TryRemove(args.DeliveryTag, out var tcs))
                    tcs.TrySetResult(false);
            }
            await Task.CompletedTask;
        };
    }

    public async Task<PublishResult> PublishAsync<T>(string routingKey, T message) where T : class
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var messageId = Guid.NewGuid().ToString();
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = messageId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            ContentType = "application/json"
        };

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            await _publishLock.WaitAsync();
            try
            {
                var seqNo = await _channel.GetNextPublishSequenceNumberAsync();
                _pendingConfirms[seqNo] = tcs;

                // Publish the message normally
                await _channel.BasicPublishAsync(
                    exchange: _exchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body
                );
            }
            finally
            {
                _publishLock.Release();
            }

            // Wait for broker confirmation
            var success = await tcs.Task;
            return success ? PublishResult.Success : PublishResult.FailedBrokerError;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to publish message {MessageId} to routing key {RoutingKey}", messageId, routingKey);
            return PublishResult.FailedBrokerError;
        }
    }

    public void Dispose()
    {
        _channel?.CloseAsync().GetAwaiter().GetResult();
        _channel?.Dispose();
    }
}
