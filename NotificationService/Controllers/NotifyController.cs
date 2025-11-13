namespace NotificationService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotifyController : ControllerBase
    {
        private readonly ILogger<NotifyController> _logger;

        public NotifyController(ILogger<NotifyController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public IActionResult Notify([FromBody] GradeEvent gradeEvent)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                string processedData = $"Студент '{gradeEvent.StudentName}' отримав '{gradeEvent.Grade}' з предмету '{gradeEvent.Subject}'.";
                _logger.LogInformation($"[ORCHESTRATION] Отримано прямий HTTP-запит.");
                _logger.LogInformation($"   => Оброблено: {processedData}");
                _logger.LogInformation($"   [NOTIFY] Email-сповіщення надіслано для {gradeEvent.StudentName}");

                return Ok(new { status = "Notification sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ORCHESTRATION] Помилка при обробці HTTP-запиту.");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}