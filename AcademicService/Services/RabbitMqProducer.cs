using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace AcademicService.Services
{
    public class RabbitMqProducer : IRabbitMqProducer
    {
        private readonly IConnection _connection;
        private readonly ILogger<RabbitMqProducer> _logger;
        private readonly ActivitySource _activitySource;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public RabbitMqProducer(
            IConnection connection,
            ILogger<RabbitMqProducer> logger,
            ActivitySource activitySource)
        {
            _connection = connection;
            _logger = logger;
            _activitySource = activitySource;
        }

        public async Task PublishMessageAsync<T>(T message, string exchangeName, string routingKey)
        {
            await using var channel = await _connection.CreateChannelAsync();

            try
            {
                await channel.ExchangeDeclareAsync(
                    exchange: exchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                string messageBody = JsonSerializer.Serialize(message, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(messageBody);

                using var activity = _activitySource.StartPublishActivity(
                    exchangeName,
                    routingKey,
                    body.Length);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    Headers = new Dictionary<string, object?>()
                };

                properties.InjectTraceContext();

                await channel.BasicPublishAsync(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                activity.SetSuccess();

                _logger.LogInformation(
                    $"[RabbitMQ] Надіслано в '{exchangeName}' (ключ: '{routingKey}'): {messageBody}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"[RabbitMQ] Помилка при надсиланні в '{exchangeName}' (ключ: '{routingKey}')");

                throw;
            }
        }
    }
}