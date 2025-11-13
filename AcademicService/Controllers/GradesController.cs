using AcademicService.Services;

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

            gradeEvent.Timestamp = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("   => Крок 1: Здійснення прямого HTTP-дзвінка до NotificationService...");

                var httpClient = _httpClientFactory.CreateClient();

                var notificationServiceUrl = "http://localhost:5000/api/notify";

                var response = await httpClient.PostAsJsonAsync(notificationServiceUrl, gradeEvent);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("   => Крок 2: NotificationService відповів успіхом.");
                }
                else
                {
                    _logger.LogWarning($"   => Крок 2: NotificationService відповів помилкою: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "   => Крок 2: Не вдалося зв'язатися з NotificationService.");
            }

            _logger.LogInformation("   => Крок 4: Повернення відповіді клієнту.");
            return CreatedAtAction(nameof(AddGradeOrchestrated), gradeEvent);
        }
    }
}