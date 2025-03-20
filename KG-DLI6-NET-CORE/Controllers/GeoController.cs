using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("api/geo")]
    public class GeoController : ControllerBase
    {
        private readonly GeoService _geoService;
        private readonly ILogger<GeoController> _logger;

        public GeoController(
            GeoService geoService,
            ILogger<GeoController> logger)
        {
            _geoService = geoService;
            _logger = logger;
        }

        [HttpPost("process-geo-data")]
        public async Task<IActionResult> ProcessGeoData()
        {
            _logger.LogInformation("Начало процесса обработки географических данных");
            
            try
            {
                var result = await _geoService.ProcessGeoDataAsync();
                
                return Ok(new 
                { 
                    success = true, 
                    message = $"Обработка географических данных завершена успешно. Обработано {result.Count} записей.",
                    count = result.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке географических данных");
                return StatusCode(500, new { success = false, message = $"Ошибка при обработке географических данных: {ex.Message}" });
            }
        }
    }
}