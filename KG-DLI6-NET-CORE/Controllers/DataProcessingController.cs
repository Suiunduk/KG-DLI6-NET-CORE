// Controllers/DataProcessingController.cs
using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DataProcessingController : ControllerBase
    {
        private readonly DataService _dataService;
        private readonly ILogger<DataProcessingController> _logger;

        public DataProcessingController(DataService dataService, ILogger<DataProcessingController> logger)
        {
            _dataService = dataService;
            _logger = logger;
        }

        [HttpPost("process-population-visit-data")]
        public async Task<IActionResult> ProcessPopulationVisitData()
        {
            try
            {
                _logger.LogInformation("Starting population and visit data processing");
                var result = await _dataService.ProcessPopulationVisitDataAsync();
                return Ok(new { message = "Data processed successfully", count = result.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing population and visit data");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}