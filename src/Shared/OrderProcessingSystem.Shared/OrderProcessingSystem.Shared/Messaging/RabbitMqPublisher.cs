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
    
    // Thread-safe dictionary to track pending publish operations
    // Key: messageId (unique identifier for each message)
    // Value: TaskCompletionSource that will be completed when we know the routing result
    // This allows the callback thread to signal the publishing thread
    private readonly ConcurrentDictionary<string, TaskCompletionSource<PublishResult>> _pendingReturns = new();

    public RabbitMqPublisher(IConnection connection, string exchangeName, ILogger<RabbitMqPublisher>? logger = null)
    {
        _channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
        _exchangeName = exchangeName;
        _logger = logger;

        // Declare exchange as Topic for flexible routing
        _channel.ExchangeDeclareAsync(
            exchange: _exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false
        ).GetAwaiter().GetResult();

        // Enable publisher confirms - broker will acknowledge each published message
        // This ensures we know the broker received the message (not just that we sent it)
        _channel.ConfirmSelectAsync().GetAwaiter().GetResult();

        // Register callback for unroutable messages
        // RabbitMQ fires this event when a message with mandatory=true cannot be routed to any queue
        // This runs on a different thread from PublishAsync
        _channel.BasicReturnAsync += (sender, args) =>
        {
            var messageId = args.BasicProperties.MessageId;
            
            // Find the TaskCompletionSource for this message and remove it from tracking
            if (_pendingReturns.TryRemove(messageId, out var tcs))
            {
                // Signal the waiting PublishAsync that the message failed to route
                // This completes the tcs.Task with FailedNoRoute result
                tcs.SetResult(PublishResult.FailedNoRoute);
                
                _logger?.LogWarning(
                    "Message {MessageId} was returned: ReplyCode={ReplyCode}, ReplyText={ReplyText}, RoutingKey={RoutingKey}",
                    messageId, args.ReplyCode, args.ReplyText, args.RoutingKey);
            }
            
            return Task.CompletedTask;
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

        // Create a TaskCompletionSource - a "promise" that can be completed manually
        // Think of it as a remote control for a Task - we can complete it from anywhere
        // The callback thread will call tcs.SetResult() to complete this
        var tcs = new TaskCompletionSource<PublishResult>();
        
        // Register this message's TaskCompletionSource before publishing
        // The callback will use messageId to find and complete the right TCS
        _pendingReturns[messageId] = tcs;

        try
        {
            // Publish the message with mandatory=true
            // This tells RabbitMQ: "notify me if you can't route this to any queue"
            await _channel.BasicPublishAsync(
                exchange: _exchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body
            );

            // Wait for broker confirmation (part 1 of reliability)
            // This ensures the broker received and processed the message
            // Throws if broker rejects (disk full, etc.)
            await _channel.WaitForConfirmsOrDieAsync();

            // Important: WaitForConfirmsOrDieAsync only confirms broker accepted the message
            // It does NOT guarantee routing is complete. The BasicReturnAsync callback
            // might fire slightly after this returns.
            // 
            // Solution: Wait briefly for the callback to fire if the message is unroutable
            // Most unroutable messages trigger callback within milliseconds
            // Use a short timeout to balance reliability vs performance
            
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            try
            {
                // Wait for either:
                // 1. Return callback fires (message unroutable) - tcs.Task completes
                // 2. 100ms timeout (message successfully routed)
                await tcs.Task.WaitAsync(cts.Token);
                
                // If we get here, callback fired - message was unroutable
                _pendingReturns.TryRemove(messageId, out _);
                return await tcs.Task; // Returns FailedNoRoute
            }
            catch (TimeoutException)
            {
                // Timeout = callback didn't fire = message successfully routed
                _pendingReturns.TryRemove(messageId, out _);
                return PublishResult.Success;
            }
        }
        catch (Exception ex)
        {
            // Clean up tracking and return error
            _pendingReturns.TryRemove(messageId, out _);
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
