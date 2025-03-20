using KG_DLI6_NET_CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace KG_DLI6_NET_CORE.Controllers
{
    [ApiController]
    [Route("api/budget-rebalancing")]
    public class BudgetRebalancingController : ControllerBase
    {
        private readonly BudgetRebalancingService _budgetRebalancingService;
        private readonly ILogger<BudgetRebalancingController> _logger;

        public BudgetRebalancingController(
            BudgetRebalancingService budgetRebalancingService,
            ILogger<BudgetRebalancingController> logger)
        {
            _budgetRebalancingService = budgetRebalancingService;
            _logger = logger;
        }

        [HttpPost("rebalance")]
        public async Task<IActionResult> RebalanceBudget([FromBody] RebalanceRequest request)
        {
            _logger.LogInformation($"Начало процесса ребалансировки бюджета с максимальным снижением {request.DownMaxPercentage}%");
            
            try
            {
                var result = await _budgetRebalancingService.RebalanceBudgetAsync(request.DownMaxPercentage);
                
                return Ok(new 
                { 
                    success = true, 
                    message = $"Ребалансировка бюджета завершена успешно.",
                    result = new
                    {
                        totalBudget = result.TotalBudget,
                        downMaxPercentage = result.DownMaxPercentage,
                        upMaxPercentage = result.UpMaxPercentage,
                        shortfallTotal = result.ShortfallTotal,
                        excessTotal = result.ExcessTotal,
                        dataCount = result.Data.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при ребалансировке бюджета");
                return StatusCode(500, new { success = false, message = $"Ошибка при ребалансировке бюджета: {ex.Message}" });
            }
        }
    }
    
    public class RebalanceRequest
    {
        public double DownMaxPercentage { get; set; } = 10.0; // Значение по умолчанию - 10%
    }
}