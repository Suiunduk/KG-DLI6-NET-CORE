using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DistanceCalculationController : ControllerBase
    {
        private readonly DistanceCalculationService _distanceService;
        private readonly ILogger<DistanceCalculationController> _logger;

        public DistanceCalculationController(
            DistanceCalculationService distanceService,
            ILogger<DistanceCalculationController> logger)
        {
            _distanceService = distanceService;
            _logger = logger;
        }

        [HttpPost("calculate-distances")]
        public async Task<IActionResult> CalculateDistances()
        {
            _logger.LogInformation("Начало расчета расстояний между медицинскими организациями");
            try
            {
                var organizations = await _distanceService.CalculateDistancesAsync();
                return Ok(new { message = $"Успешно рассчитаны расстояния для {organizations.Count} медицинских организаций" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Ошибка при расчете расстояний");
                return StatusCode(500, new { error = "Ошибка при расчете расстояний" });
            }
        }
    }
}