using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("api/budget")]
    public class BudgetController : ControllerBase
    {
        private readonly BudgetReplicationService _budgetReplicationService;
        private readonly ILogger<BudgetController> _logger;

        public BudgetController(
            BudgetReplicationService budgetReplicationService,
            ILogger<BudgetController> logger)
        {
            _budgetReplicationService = budgetReplicationService;
            _logger = logger;
        }

        [HttpPost("replicate-old-budgets")]
        public async Task<IActionResult> ReplicateOldBudgets()
        {
            _logger.LogInformation("Начало процесса воспроизведения старых бюджетов");
            
            try
            {
                var result = await _budgetReplicationService.ReplicateOldBudgetsAsync();
                
                return Ok(new 
                { 
                    success = true, 
                    message = $"Воспроизведение старых бюджетов завершено успешно. Обработано {result.Count} организаций.",
                    count = result.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при воспроизведении старых бюджетов");
                return StatusCode(500, new { success = false, message = $"Ошибка при воспроизведении старых бюджетов: {ex.Message}" });
            }
        }
    }
}