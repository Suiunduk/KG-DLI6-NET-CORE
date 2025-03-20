using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace KG_DLI6_NET_CORE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoefficientsController : ControllerBase
    {
        private readonly DataService _dataService;
        private readonly AgeSexCoefficientService _ageSexCoefficientService;
        private readonly WorkloadCoefficientService _workloadCoefficientService;
        private readonly WorkloadGraphService _workloadGraphService;
        private readonly ILogger<CoefficientsController> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public CoefficientsController(
            DataService dataService,
            AgeSexCoefficientService ageSexCoefficientService,
            WorkloadCoefficientService workloadCoefficientService,
            WorkloadGraphService workloadGraphService,
            ILogger<CoefficientsController> logger,
            IWebHostEnvironment hostingEnvironment)
        {
            _dataService = dataService;
            _ageSexCoefficientService = ageSexCoefficientService;
            _workloadCoefficientService = workloadCoefficientService;
            _workloadGraphService = workloadGraphService;
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpPost("population-visit-data")]
        public async Task<IActionResult> ProcessPopulationVisitData()
        {
            try
            {
                _logger.LogInformation("Начало обработки данных о населении и посещениях");
                var result = await _dataService.ProcessPopulationVisitDataAsync();
                _logger.LogInformation("Обработка данных о населении и посещениях завершена");
                return Ok(new { message = "Данные о населении и посещениях успешно обработаны", count = result.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке данных о населении и посещениях");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("age-sex-coefficients")]
        public async Task<IActionResult> CalculateAgeSexCoefficients()
        {
            try
            {
                _logger.LogInformation("Начало расчета возрастно-половых коэффициентов");
                await _ageSexCoefficientService.CalculateAgeSexCoefficientsAsync();
                _logger.LogInformation("Расчет возрастно-половых коэффициентов завершен");
                return Ok(new { message = "Возрастно-половые коэффициенты успешно рассчитаны" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при расчете возрастно-половых коэффициентов");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        
        [HttpPost("workload-coefficients")]
        public async Task<IActionResult> CalculateWorkloadCoefficients([FromQuery] double? upMax, [FromQuery] double? downMax)
        {
            try
            {
                _logger.LogInformation("Начало расчета коэффициентов нагрузки");
                var result = await _workloadCoefficientService.CalculateWorkloadCoefficientsAsync(upMax, downMax);
                _logger.LogInformation("Расчет коэффициентов нагрузки завершен");
                return Ok(new { message = "Коэффициенты нагрузки успешно рассчитаны", count = result.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при расчете коэффициентов нагрузки");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        
        [HttpPost("workload-graphs")]
        public async Task<IActionResult> CreateWorkloadGraphs()
        {
            try
            {
                _logger.LogInformation("Начало создания графиков рабочей нагрузки");
                await _workloadGraphService.CreateWorkloadGraphsAsync();
                _logger.LogInformation("Создание графиков рабочей нагрузки завершено");
                
                // Формируем URL для доступа к графикам
                var request = HttpContext.Request;
                var baseUrl = $"{request.Scheme}://{request.Host}";
                
                var graphUrl1 = $"{baseUrl}/output/Fig3-WorkloadCoefficient.png";
                var graphUrl2 = $"{baseUrl}/output/Fig4-AdjWorkloadCoefficient.png";
                
                return Ok(new { 
                    message = "Графики рабочей нагрузки успешно созданы", 
                    graphs = new[] 
                    { 
                        new { 
                            name = "Коэффициент загруженности по ОЗ (2022)",
                            url = graphUrl1 
                        },
                        new { 
                            name = "Коэффициент загруженности по ОЗ, с ограничениями (2022)",
                            url = graphUrl2 
                        }
                    } 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании графиков рабочей нагрузки");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        
        [HttpGet("graphs/{fileName}")]
        public IActionResult GetGraph(string fileName)
        {
            try
            {
                var outputPath = Path.Combine(_hostingEnvironment.ContentRootPath, "output");
                var filePath = Path.Combine(outputPath, fileName);
                
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning($"Файл {fileName} не найден");
                    return NotFound(new { error = $"Файл {fileName} не найден" });
                }
                
                // Определяем тип MIME на основе расширения файла
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                var mimeType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".pdf" => "application/pdf",
                    _ => "application/octet-stream"
                };
                
                // Возвращаем файл
                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                return new FileStreamResult(fileStream, mimeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении файла {fileName}");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}