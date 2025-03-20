using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace KG_DLI6_NET_CORE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HcoController : ControllerBase
    {
        private readonly HcoMergeService _hcoMergeService;
        private readonly ILogger<HcoController> _logger;

        public HcoController(
            HcoMergeService hcoMergeService,
            ILogger<HcoController> logger)
        {
            _hcoMergeService = hcoMergeService;
            _logger = logger;
        }

        [HttpPost("merge-data")]
        public async Task<IActionResult> MergeHcoData()
        {
            try
            {
                _logger.LogInformation("Начало объединения данных о медицинских учреждениях");
                var result = await _hcoMergeService.MergeHcoDataAsync();
                _logger.LogInformation("Объединение данных о медицинских учреждениях завершено");
                return Ok(new { 
                    message = "Данные о медицинских учреждениях успешно объединены", 
                    count = result.Count 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при объединении данных о медицинских учреждениях");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}