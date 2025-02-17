using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using FansVoice.EventService.Interfaces;

namespace FansVoice.EventService.Services
{
    public class MessageBusService : IMessageBusService, IDisposable
    {
        private readonly ILogger<MessageBusService> _logger;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ICircuitBreakerService _circuitBreaker;
        private bool _disposed;

        public MessageBusService(
            ILogger<MessageBusService> logger,
            ICircuitBreakerService circuitBreaker,
            string hostName = "localhost",
            string userName = "guest",
            string password = "guest")
        {
            _logger = logger;
            _circuitBreaker = circuitBreaker;

            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = hostName,
                    UserName = userName,
                    Password = password,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _connection.ConnectionShutdown += OnConnectionShutdown;
                _connection.ConnectionBlocked += OnConnectionBlocked;
                _connection.ConnectionUnblocked += OnConnectionUnblocked;

                _logger.LogInformation("Successfully connected to RabbitMQ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        public async Task<bool> PublishAsync<T>(string topic, T message)
        {
            return await _circuitBreaker.ExecuteAsync($"publish_{topic}", async () =>
            {
                try
                {
                    EnsureExchangeExists(topic);

                    var messageJson = JsonSerializer.Serialize(message);
                    var body = Encoding.UTF8.GetBytes(messageJson);

                    _channel.BasicPublish(
                        exchange: topic,
                        routingKey: "",
                        basicProperties: null,
                        body: body);

                    _logger.LogInformation("Message published to topic {Topic}: {Message}", topic, messageJson);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish message to topic {Topic}", topic);
                    throw;
                }
            });
        }

        public async Task<bool> PublishWithRetryAsync<T>(string topic, T message, int retryCount = 3)
        {
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    return await PublishAsync(topic, message);
                }
                catch (Exception ex)
                {
                    if (i == retryCount - 1) throw;

                    var delay = TimeSpan.FromSeconds(Math.Pow(2, i)); // Exponential backoff
                    _logger.LogWarning(ex, "Retry {Attempt} of {MaxRetries} failed for topic {Topic}. Waiting {Delay}s before next attempt",
                        i + 1, retryCount, topic, delay.TotalSeconds);

                    await Task.Delay(delay);
                }
            }

            return false;
        }

        public async Task SubscribeAsync<T>(string topic, Func<T, Task> handler)
        {
            try
            {
                EnsureExchangeExists(topic);

                var queueName = _channel.QueueDeclare().QueueName;
                _channel.QueueBind(queueName, topic, "");

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(body));

                        await handler(message);
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from topic {Topic}", topic);
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                _channel.BasicConsume(queue: queueName,
                                    autoAck: false,
                                    consumer: consumer);

                _logger.LogInformation("Subscribed to topic {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to topic {Topic}", topic);
                throw;
            }
        }

        public async Task UnsubscribeAsync(string topic)
        {
            try
            {
                // In RabbitMQ, we can't directly unsubscribe from a topic
                // Instead, we can delete the queue or stop consuming
                // Here we'll just close the channel for the topic
                _channel.Close();
                _logger.LogInformation("Unsubscribed from topic {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from topic {Topic}", topic);
                throw;
            }
        }

        public async Task<bool> IsConnectedAsync()
        {
            return _connection?.IsOpen == true && _channel?.IsOpen == true;
        }

        public async Task ConnectAsync()
        {
            if (!await IsConnectedAsync())
            {
                throw new InvalidOperationException("Already connected or disposed");
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_channel?.IsOpen == true)
                {
                    await _channel.CloseAsync();
                }

                if (_connection?.IsOpen == true)
                {
                    await _connection.CloseAsync();
                }

                _logger.LogInformation("Disconnected from RabbitMQ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from RabbitMQ");
                throw;
            }
        }

        public async Task CreateExchangeAsync(string exchangeName, string exchangeType = "topic")
        {
            try
            {
                _channel.ExchangeDeclare(
                    exchange: exchangeName,
                    type: exchangeType,
                    durable: true,
                    autoDelete: false);

                _logger.LogInformation("Exchange {ExchangeName} created with type {ExchangeType}",
                    exchangeName, exchangeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create exchange {ExchangeName}", exchangeName);
                throw;
            }
        }

        public async Task CreateQueueAsync(string queueName, bool durable = true)
        {
            try
            {
                _channel.QueueDeclare(
                    queue: queueName,
                    durable: durable,
                    exclusive: false,
                    autoDelete: false);

                _logger.LogInformation("Queue {QueueName} created", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create queue {QueueName}", queueName);
                throw;
            }
        }

        public async Task BindQueueAsync(string queueName, string exchangeName, string routingKey)
        {
            try
            {
                _channel.QueueBind(
                    queue: queueName,
                    exchange: exchangeName,
                    routingKey: routingKey);

                _logger.LogInformation("Queue {QueueName} bound to exchange {ExchangeName} with routing key {RoutingKey}",
                    queueName, exchangeName, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bind queue {QueueName} to exchange {ExchangeName}",
                    queueName, exchangeName);
                throw;
            }
        }

        private void EnsureExchangeExists(string exchangeName)
        {
            _channel.ExchangeDeclare(
                exchange: exchangeName,
                type: "topic",
                durable: true,
                autoDelete: false);
        }

        private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection is blocked. Reason: {Reason}", e.Reason);
        }

        private void OnConnectionUnblocked(object sender, EventArgs e)
        {
            _logger.LogInformation("RabbitMQ connection is unblocked");
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection is shut down. Reason: {Reason}", e.Reason);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }

            _disposed = true;
        }
    }
}