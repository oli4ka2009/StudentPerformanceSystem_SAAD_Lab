using RabbitMQ.Client;
using System.Text.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace AcademicService.Services
{
    public class RabbitMqProducer : IRabbitMqProducer, IAsyncDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel; // IModel замінено на IChannel
        private readonly ILogger<RabbitMqProducer> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public RabbitMqProducer(IConfiguration configuration, ILogger<RabbitMqProducer> logger)
        {
            _logger = logger;
            try
            {
                var hostName = configuration["RabbitMq:HostName"];
                if (string.IsNullOrEmpty(hostName))
                {
                    throw new InvalidOperationException("RabbitMq:HostName не знайдено в конфігурації.");
                }

                var factory = new ConnectionFactory() { HostName = hostName };

                // CreateConnectionAsync замість CreateConnection
                _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();

                // CreateChannelAsync замість CreateModel
                _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

                _logger.LogInformation("RabbitMQ Producer: Успішно підключено.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ Producer: Не вдалося підключитися.");
                throw;
            }
        }

        public async Task PublishMessageAsync<T>(T message, string exchangeName, string routingKey)
        {
            // ExchangeDeclareAsync замість ExchangeDeclare
            await _channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            string messageBody = JsonSerializer.Serialize(message, _jsonOptions);
            var body = Encoding.UTF8.GetBytes(messageBody);

            // BasicPublishAsync замість BasicPublish
            await _channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                body: body);

            _logger.LogInformation($"[RabbitMQ] Надіслано в {exchangeName} (ключ: {routingKey}): {messageBody}");
        }

        // Асинхронний Dispose
        public async ValueTask DisposeAsync()
        {
            if (_channel != null)
                await _channel.CloseAsync();

            if (_connection != null)
                await _connection.CloseAsync();
        }
    }
}