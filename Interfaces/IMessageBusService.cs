using System;
using System.Threading.Tasks;

namespace FansVoice.EventService.Interfaces
{
    public interface IMessageBusService
    {
        Task PublishAsync<T>(string topic, T message) where T : class;
        Task PublishWithRetryAsync<T>(string topic, T message, int retryCount = 3) where T : class;
        Task SubscribeAsync<T>(string topic, Func<T, Task> handler) where T : class;
        Task UnsubscribeAsync(string topic);
        Task<bool> IsConnectedAsync();
        Task ConnectAsync();
        Task DisconnectAsync();
        Task CreateExchangeAsync(string exchangeName, string exchangeType = "topic");
        Task CreateQueueAsync(string queueName, bool durable = true);
        Task BindQueueAsync(string queueName, string exchangeName, string routingKey);
    }
}