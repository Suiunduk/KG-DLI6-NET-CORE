using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthcareDataEnrichmentController : ControllerBase
    {
        private readonly HealthcareDataEnrichmentService _enrichmentService;
        private readonly ILogger<HealthcareDataEnrichmentController> _logger;

        public HealthcareDataEnrichmentController(
            HealthcareDataEnrichmentService enrichmentService,
            ILogger<HealthcareDataEnrichmentController> logger)
        {
            _enrichmentService = enrichmentService;
            _logger = logger;
        }

        [HttpPost("enrich-data")]
        public async Task<IActionResult> EnrichData()
        {
            _logger.LogInformation("Начало обогащения данных о медицинских организациях");
            try
            {
                var organizations = await _enrichmentService.EnrichHealthcareDataAsync();
                return Ok(new { message = $"Успешно обогащены данные для {organizations.Count} медицинских организаций" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обогащении данных о медицинских организациях");
                return StatusCode(500, new { error = "Ошибка при обогащении данных о медицинских организациях" });
            }
        }
    }
}