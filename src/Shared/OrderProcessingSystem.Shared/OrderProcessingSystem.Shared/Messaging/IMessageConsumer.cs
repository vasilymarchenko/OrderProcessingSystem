namespace OrderProcessingSystem.Shared.Messaging;

public interface IMessageConsumer
{
    Task SubscribeAsync<T>(string queueName, string routingKey, Func<T, Task> handler) where T : class;
}
