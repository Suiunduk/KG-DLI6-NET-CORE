using System;
using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GeoScenarioController : ControllerBase
    {
        private readonly GeoScenarioService _geoScenarioService;
        private readonly ILogger<GeoScenarioController> _logger;

        public GeoScenarioController(GeoScenarioService geoScenarioService, ILogger<GeoScenarioController> logger)
        {
            _geoScenarioService = geoScenarioService;
            _logger = logger;
        }

        [HttpPost("create-scenarios")]
        public async Task<IActionResult> CreateScenarios()
        {
            _logger.LogInformation("Начало создания географических сценариев");
            
            try
            {
                var organizations = await _geoScenarioService.CreateGeoScenariosAsync();
                return Ok(new { 
                    message = "Географические сценарии успешно созданы",
                    organizationsCount = organizations.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании географических сценариев");
                return StatusCode(500, new { error = "Ошибка при создании географических сценариев", details = ex.Message });
            }
        }
    }
}