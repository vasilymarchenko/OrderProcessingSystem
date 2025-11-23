namespace OrderProcessingSystem.Shared.Messaging;

public enum PublishResult
{
    Success,
    FailedNoRoute,
    FailedBrokerError
}

public interface IMessagePublisher
{
    Task<PublishResult> PublishAsync<T>(string routingKey, T message) where T : class;
}
