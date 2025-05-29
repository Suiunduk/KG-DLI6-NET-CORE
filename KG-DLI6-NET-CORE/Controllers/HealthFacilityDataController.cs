using System;
using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthFacilityDataController : ControllerBase
    {
        private readonly HealthFacilityDataService _healthFacilityDataService;
        private readonly ILogger<HealthFacilityDataController> _logger;

        public HealthFacilityDataController(HealthFacilityDataService healthFacilityDataService, ILogger<HealthFacilityDataController> logger)
        {
            _healthFacilityDataService = healthFacilityDataService;
            _logger = logger;
        }

        [HttpPost("process-health-facility-data")]
        public async Task<IActionResult> ProcessHealthFacilityData()
        {
            _logger.LogInformation("Начало обработки данных медицинских учреждений");
            
            try
            {
                var facilities = await _healthFacilityDataService.ProcessHealthFacilityDataAsync();
                return Ok(new { 
                    message = "Данные медицинских учреждений успешно обработаны",
                    recordsCount = facilities.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке данных медицинских учреждений");
                return StatusCode(500, new { error = "Ошибка при обработке данных медицинских учреждений", details = ex.Message });
            }
        }
    }
}