using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SharedModels;
using System.Text;
using System.Text.Json;

namespace NotificationService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private IConnection _connection;
        private IChannel _channel;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            try
            {
                var hostName = _configuration["RabbitMq:HostName"];
                if (string.IsNullOrEmpty(hostName))
                {
                    throw new InvalidOperationException("RabbitMq:HostName не знайдено в конфігурації.");
                }

                var factory = new ConnectionFactory() { HostName = hostName };
                _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
                _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

                _logger.LogInformation("NotificationService: Успішно підключено до RabbitMQ.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotificationService: Не вдалося підключитися до RabbitMQ.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null)
            {
                _logger.LogError("RabbitMQ канал не ініціалізовано. Worker не може запуститися.");
                return;
            }

            await _channel.ExchangeDeclareAsync(
                exchange: "grades_exchange",
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            var queueName = (await _channel.QueueDeclareAsync(queue: "", exclusive: true)).QueueName;
            var bindingKey = "grades.new.*";

            await _channel.QueueBindAsync(
                queue: queueName,
                exchange: "grades_exchange",
                routingKey: bindingKey);

            _logger.LogInformation($"[LOG] Черга '{queueName}' прив'язана до 'grades_exchange' з ключем '{bindingKey}'.");
            _logger.LogInformation("[*] Очікування на нові оцінки...");

            // ВИПРАВЛЕННЯ: Використовуємо AsyncEventingBasicConsumer замість EventingBasicConsumer
            var consumer = new AsyncEventingBasicConsumer(_channel);

            // ВИПРАВЛЕННЯ: Обробник тепер асинхронний (async)
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var routingKey = ea.RoutingKey;

                    _logger.LogInformation($"\n[LOG] Отримано повідомлення з ключем '{routingKey}'.");

                    var gradeEvent = JsonSerializer.Deserialize<GradeEvent>(message);

                    string processedData = $"Студент '{gradeEvent.StudentName}' отримав '{gradeEvent.Grade}' з предмету '{gradeEvent.Subject}'.";
                    _logger.LogInformation($"   => Оброблено: {processedData}");

                    _logger.LogInformation($"   [NOTIFY] Email-сповіщення надіслано для {gradeEvent.StudentName}");
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "[ERROR] Не вдалося десеріалізувати повідомлення.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ERROR] Помилка при обробці повідомлення.");
                }

                // Важливо повернути Task
                await Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: true,
                consumer: consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel != null) await _channel.CloseAsync();
            if (_connection != null) await _connection.CloseAsync();
            _logger.LogInformation("NotificationService зупинено.");
            await base.StopAsync(cancellationToken);
        }
    }
}