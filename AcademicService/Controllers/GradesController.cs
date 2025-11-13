using AcademicService.Services;
using Microsoft.AspNetCore.Mvc;
using SharedModels;

namespace AcademicService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GradesController : ControllerBase
    {
        private readonly IRabbitMqProducer _rabbitProducer;
        private readonly ILogger<GradesController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public GradesController(IRabbitMqProducer rabbitProducer, ILogger<GradesController> logger, IHttpClientFactory httpClientFactory)
        {
            _rabbitProducer = rabbitProducer;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("choreography")]
        public async Task<IActionResult> AddGrade([FromBody] GradeEvent gradeEvent)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                gradeEvent.Timestamp = DateTime.UtcNow;
                var routingKey = $"grades.new.{gradeEvent.Subject.ToLower().Trim()}";
                var exchangeName = "grades_exchange";

                await _rabbitProducer.PublishMessageAsync(gradeEvent, exchangeName, routingKey);

                _logger.LogInformation($"Прийнято запит на оцінку (Хореографія) для {gradeEvent.StudentName}.");

                return Accepted(gradeEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при відправці повідомлення в RabbitMQ");
                return StatusCode(503, "Сервіс повідомлень тимчасово недоступний.");
            }
        }

        [HttpPost("orchestration")]
        public async Task<IActionResult> AddGradeOrchestrated([FromBody] GradeEvent gradeEvent)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation($"Прийнято запит на оцінку (Оркестрація) для {gradeEvent.StudentName}.");

            // КРОК 1: (Симуляція) Збереження в локальну базу
            // ... уявимо, що ми зберегли оцінку в AcademicDB ...
            gradeEvent.Timestamp = DateTime.UtcNow;
            _logger.LogInformation("   => Крок 1: Оцінку 'збережено' в локальну БД.");


            // КРОК 2: Прямий виклик NotificationService
            try
            {
                _logger.LogInformation("   => Крок 2: Здійснення прямого HTTP-дзвінка до NotificationService...");

                // Створюємо клієнт
                var httpClient = _httpClientFactory.CreateClient();

                // ВАЖЛИВО: "http://localhost:XXXX" - це адреса, на якій запуститься
                // ваш NotificationService. Вам потрібно буде подивитися її в консолі
                // і, можливо, змінити цей порт.
                // Ми використовуємо "http", а не "https", щоб спростити тест.
                // Перевірте файл /Properties/launchSettings.json у NotificationService

                // TODO: Замініть порт 7001 на реальний порт вашого NotificationService
                var notificationServiceUrl = "http://localhost:5000/api/notify";

                // Здійснюємо POST-запит і ЧЕКАЄМО на відповідь
                var response = await httpClient.PostAsJsonAsync(notificationServiceUrl, gradeEvent);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("   => Крок 3: NotificationService відповів успіхом.");
                }
                else
                {
                    // Якщо сервіс сповіщень "впав", ми дізнаємося про це тут
                    _logger.LogWarning($"   => Крок 3: NotificationService відповів помилкою: {response.StatusCode}");
                    // У реальному житті тут може бути логіка компенсації
                }
            }
            catch (Exception ex)
            {
                // Якщо NotificationService взагалі не запущений, ми потрапимо сюди
                _logger.LogError(ex, "   => Крок 3: Не вдалося зв'язатися з NotificationService.");
                // Ми все одно повертаємо успіх, оскільки оцінку ЗБЕРЕЖЕНО.
                // Але логуємо, що сповіщення не надіслано.
            }

            // КРОК 4: Повертаємо відповідь клієнту
            // Ми повертаємо 201 Created, оскільки головна робота (збереження) виконана.
            _logger.LogInformation("   => Крок 4: Повернення відповіді клієнту.");
            return CreatedAtAction(nameof(AddGradeOrchestrated), gradeEvent);
        }
    }
}