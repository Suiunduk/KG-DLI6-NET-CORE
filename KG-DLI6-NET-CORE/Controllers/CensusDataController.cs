using System;
using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CensusDataController : ControllerBase
    {
        private readonly CensusDataService _censusDataService;
        private readonly ILogger<CensusDataController> _logger;

        public CensusDataController(CensusDataService censusDataService, ILogger<CensusDataController> logger)
        {
            _censusDataService = censusDataService;
            _logger = logger;
        }

        [HttpPost("process-census-data")]
        public async Task<IActionResult> ProcessCensusData()
        {
            _logger.LogInformation("Начало обработки данных переписи населения");
            
            try
            {
                var censusData = await _censusDataService.ProcessCensusDataAsync();
                return Ok(new { 
                    message = "Данные переписи населения успешно обработаны",
                    recordsCount = censusData.Count,
                    totalPopulation = censusData.Sum(d => d.CorrectedPopulation)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке данных переписи населения");
                return StatusCode(500, new { error = "Ошибка при обработке данных переписи населения", details = ex.Message });
            }
        }
    }
}