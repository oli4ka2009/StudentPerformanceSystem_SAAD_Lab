namespace NotificationService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConnection _connection;
        private readonly ActivitySource _activitySource;

        public Worker(ILogger<Worker> logger, IConnection connection, ActivitySource activitySource)
        {
            _logger = logger;
            _connection = connection;
            _activitySource = activitySource;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var channel = await _connection.CreateChannelAsync();

            try
            {
                await channel.ExchangeDeclareAsync(
                    exchange: "grades_exchange",
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                var queueName = (await channel.QueueDeclareAsync(queue: "", exclusive: true)).QueueName;
                var bindingKey = "grades.new.*";

                await channel.QueueBindAsync(
                    queue: queueName,
                    exchange: "grades_exchange",
                    routingKey: bindingKey);

                _logger.LogInformation($"[LOG] Черга '{queueName}' прив'язана до 'grades_exchange' з ключем '{bindingKey}'.");
                _logger.LogInformation("[*] Очікування на нові оцінки...");

                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += async (model, ea) =>
                {
                    using var activity = _activitySource.StartActivityFromMessage("ProcessGradeNotification", ea);

                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var routingKey = ea.RoutingKey;

                        _logger.LogInformation($"\n[LOG] Отримано повідомлення з ключем '{routingKey}'.");

                        var gradeEvent = JsonSerializer.Deserialize<GradeEvent>(message);

                        string processedData = $"Студент '{gradeEvent?.StudentName}' отримав '{gradeEvent?.Grade}' з предмету '{gradeEvent?.Subject}'.";
                        _logger.LogInformation($" => Оброблено: {processedData}");
                        _logger.LogInformation($" [NOTIFY] Email-сповіщення надіслано для {gradeEvent?.StudentName}");

                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "[ERROR] Не вдалося десеріалізувати повідомлення.");

                        activity.RecordError(jsonEx);

                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ERROR] ❌ Помилка при обробці повідомлення.");

                        activity.RecordError(ex);

                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("Consumer активний, очікування повідомлень...");

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            finally
            {
                await channel.CloseAsync();
                _logger.LogInformation("RabbitMQ Channel закрито.");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NotificationService зупинено.");
            await base.StopAsync(cancellationToken);
        }
    }
}