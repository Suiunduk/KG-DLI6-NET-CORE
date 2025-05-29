using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthcareOrganizationController : ControllerBase
    {
        private readonly HealthcareOrganizationService _healthcareOrganizationService;
        private readonly ILogger<HealthcareOrganizationController> _logger;

        public HealthcareOrganizationController(
            HealthcareOrganizationService healthcareOrganizationService,
            ILogger<HealthcareOrganizationController> logger)
        {
            _healthcareOrganizationService = healthcareOrganizationService;
            _logger = logger;
        }

        [HttpPost("process-organizations")]
        public async Task<IActionResult> ProcessOrganizations()
        {
            _logger.LogInformation("Начало обработки данных о медицинских организациях");
            try
            {
                var organizations = await _healthcareOrganizationService.ProcessHealthcareOrganizationsAsync();
                return Ok(new { message = $"Успешно обработано {organizations.Count} медицинских организаций" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке данных о медицинских организациях");
                return StatusCode(500, new { error = "Ошибка при обработке данных о медицинских организациях" });
            }
        }
    }
}

