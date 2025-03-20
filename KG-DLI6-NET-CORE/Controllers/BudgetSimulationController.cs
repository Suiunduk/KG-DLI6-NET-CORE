using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("api/budget-simulation")]
    public class BudgetSimulationController : ControllerBase
    {
        private readonly BudgetSimulationService _budgetSimulationService;
        private readonly ILogger<BudgetSimulationController> _logger;

        public BudgetSimulationController(
            BudgetSimulationService budgetSimulationService,
            ILogger<BudgetSimulationController> logger)
        {
            _budgetSimulationService = budgetSimulationService;
            _logger = logger;
        }

        [HttpPost("simulate-budgets")]
        public async Task<IActionResult> SimulateBudgets()
        {
            _logger.LogInformation("Начало процесса симуляции бюджетов");
            
            try
            {
                var result = await _budgetSimulationService.SimulateBudgetsAsync();
                
                return Ok(new 
                { 
                    success = true, 
                    message = $"Симуляция бюджетов завершена успешно. Обработано {result.Count} организаций.",
                    count = result.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при симуляции бюджетов");
                return StatusCode(500, new { success = false, message = $"Ошибка при симуляции бюджетов: {ex.Message}" });
            }
        }
    }
}