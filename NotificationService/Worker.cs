using OpenTelemetry.Trace;

namespace NotificationService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly ActivitySource _activitySource;
        private IConnection? _connection;
        private IChannel? _channel;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, ActivitySource activitySource)
        {
            _logger = logger;
            _configuration = configuration;
            _activitySource = activitySource;

            try
            {
                var hostName = _configuration["RabbitMq:HostName"];
                if (string.IsNullOrEmpty(hostName))
                {
                    throw new InvalidOperationException("RabbitMq:HostName не знайдено.");
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

            var consumer = new AsyncEventingBasicConsumer(_channel);

            // 🔥 ReceivedAsync замість Received, обробник асинхронний
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    ActivityContext parentContext = default;
                    if (ea.BasicProperties?.Headers?.TryGetValue("traceparent", out var tp) == true)
                    {
                        var traceParent = Encoding.UTF8.GetString((byte[])tp);
                        parentContext = ActivityContext.Parse(traceParent, null);
                    }

                    using var activity = _activitySource.StartActivity(
                        "Process Grade Notification",
                        ActivityKind.Consumer,
                        parentContext);

                    if (activity != null)
                    {
                        activity.SetTag("messaging.system", "rabbitmq");
                        _logger.LogInformation($"[TRACE] Activity створено: {activity.Id}");
                    }
                    else
                    {
                        _logger.LogWarning("[TRACE] Activity НЕ створено!");
                    }

                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var routingKey = ea.RoutingKey;

                    _logger.LogInformation($"\n[LOG] Отримано повідомлення з ключем '{routingKey}'.");

                    // Десеріалізуємо повідомлення
                    var gradeEvent = JsonSerializer.Deserialize<GradeEvent>(message);

                    string processedData = $"Студент '{gradeEvent?.StudentName}' отримав '{gradeEvent?.Grade}' з предмету '{gradeEvent?.Subject}'.";
                    _logger.LogInformation($" => Оброблено: {processedData}");
                    _logger.LogInformation($" [NOTIFY] Email-сповіщення надіслано для {gradeEvent?.StudentName}");

                    // 🔥 ВАЖЛИВО! Використовуємо await для асинхронного підтвердження
                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "[ERROR] Не вдалося десеріалізувати повідомлення.");

                    // Повідомляємо RabbitMQ про помилку (не повертаємо в чергу)
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ERROR] ❌ Помилка при обробці повідомлення.");

                    // Повідомляємо RabbitMQ про помилку (повертаємо в чергу для повторної спроби)
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false, // Ручне підтвердження для надійності
                consumer: consumer);

            _logger.LogInformation("Consumer активний, очікування повідомлень...");

            // Тримаємо Worker живим
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel != null)
            {
                await _channel.CloseAsync();
                _logger.LogInformation("RabbitMQ Channel закрито.");
            }

            if (_connection != null)
            {
                await _connection.CloseAsync();
                _logger.LogInformation("RabbitMQ Connection закрито.");
            }

            _logger.LogInformation("NotificationService зупинено.");
            await base.StopAsync(cancellationToken);
        }
    }
}