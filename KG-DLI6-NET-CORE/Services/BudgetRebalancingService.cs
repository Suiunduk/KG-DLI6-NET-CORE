using System.Text.Json;
using KG_DLI6_NET_CORE.Models;
using MathNet.Numerics;
using MathNet.Numerics.RootFinding;
using ScottPlot;

namespace KG_DLI6_NET_CORE.Services
{
    public class BudgetRebalancingService
    {
        private readonly ILogger<BudgetRebalancingService> _logger;
        private readonly string _workingPath;
        private readonly string _outputPath;
        
        // Словарь цветов для областей, аналогичный Python-коду
        private readonly Dictionary<string, System.Drawing.Color> _regionColors = new()
        {
            { "Баткенская область", System.Drawing.Color.Red },
            { "Бишкекский горкенеш", System.Drawing.Color.Blue },
            { "Чуйская область", System.Drawing.Color.Green },
            { "Иссык-Кульская область", System.Drawing.Color.Black },
            { "Джалал-Абадская область", System.Drawing.Color.Magenta },
            { "Нарынская область", System.Drawing.Color.Green },
            { "Ошская область", System.Drawing.Color.Yellow },
            { "Таласская область", System.Drawing.Color.Cyan },
            { "Ошский горкенеш", System.Drawing.Color.Black }
        };

        public BudgetRebalancingService(ILogger<BudgetRebalancingService> logger)
        {
            _logger = logger;
            
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
            
            // Создаем директории, если они не существуют
            Directory.CreateDirectory(_workingPath);
            Directory.CreateDirectory(_outputPath);
        }
        
        public async Task<RebalancingResult> RebalanceBudgetAsync(double downMaxPercentage)
        {
            _logger.LogInformation("Начало ребалансировки бюджета");
            
            // Проверка входных параметров
            if (downMaxPercentage > 100 || downMaxPercentage < 0)
            {
                _logger.LogWarning($"Указанный процент {downMaxPercentage} находится вне допустимого диапазона (0-100). Установлено значение 100%.");
                downMaxPercentage = 100;
            }
            
            // Преобразование процента в десятичное число
            double downMax = downMaxPercentage / 100;
            
            // Загрузка данных
            var simulationData = await LoadDataAsync<Dictionary<int, SimulationData>>("simulated_budgets");
            
            _logger.LogInformation($"Загружены данные симуляции: {simulationData.Count} записей");
            
            // Выбор географического коэффициента для ребалансировки
            string geokName = "geok_1"; // Используем geok_1 как в примере Python
            string budgetNewField = $"budget_new_{geokName}";
            string impactField = $"impact_{geokName}";
            
            // Расчет общего бюджета
            double totalBudget = simulationData.Values.Sum(d => d.ЦСМ_ГСВ_2023) * 1000;
            _logger.LogInformation($"Общий бюджет: {totalBudget:N2}");
            
            // Создание объекта с результатами ребалансировки
            var result = new RebalancingResult
            {
                TotalBudget = totalBudget,
                DownMaxPercentage = downMaxPercentage,
                Data = new Dictionary<int, RebalancedBudgetData>()
            };
            
            // Расчет недостачи бюджета для организаций, чей новый бюджет не соответствует критериям
            double shortfallTotal = 0;
            foreach (var item in simulationData.Values)
            {
                double budgetOld = item.ЦСМ_ГСВ_2023 * 1000;
                double budgetNew = item.GetNewBudget(budgetNewField);
                double shortfall = budgetNew >= (1 - downMax) * budgetOld ? 0 : (1 - downMax) * budgetOld - budgetNew;
                
                shortfallTotal += shortfall;
                
                // Создаем запись результата для каждой организации
                result.Data[item.Scm_NewCode] = new RebalancedBudgetData
                {
                    Scm_NewCode = item.Scm_NewCode,
                    Region = item.Region,
                    NewName = item.NewName,
                    People = item.people,
                    BudgetOld = budgetOld,
                    BudgetNew = budgetNew,
                    Impact = item.GetImpact(impactField),
                    Shortfall = shortfall
                };
            }
            
            _logger.LogInformation($"Общая недостача бюджета: {shortfallTotal:N2}");
            
            // Нахождение значения upMax, которое является бюджетно-нейтральным
            double initialGuess = downMax;
            double upMaxValue = FindBudgetNeutralUpMax(simulationData.Values.ToArray(), budgetNewField, shortfallTotal, initialGuess);
            double upMaxPercentage = upMaxValue * 100;
            
            _logger.LogInformation($"Найдено значение upMax, которое является бюджетно-нейтральным: {upMaxPercentage:N2}%");
            
            // Расчет избытка бюджета и корректировка нового бюджета
            double excessTotal = 0;
            foreach (var item in result.Data.Values)
            {
                var simData = simulationData[item.Scm_NewCode];
                double budgetOld = item.BudgetOld;
                double budgetNew = item.BudgetNew;
                
                double excess = budgetNew <= (1 + upMaxValue) * budgetOld ? 0 : budgetNew - (1 + upMaxValue) * budgetOld;
                excessTotal += excess;
                
                double budgetNewAdj = budgetNew - excess + item.Shortfall;
                double impactAdj = ((budgetNewAdj / budgetOld) - 1) * 100;
                
                // Обновляем результаты
                item.Excess = excess;
                item.BudgetNewAdj = budgetNewAdj;
                item.ImpactAdj = impactAdj;
            }
            
            _logger.LogInformation($"Общий избыток бюджета: {excessTotal:N2}");
            _logger.LogInformation($"downMax: {downMaxPercentage}%, upMax: {upMaxPercentage:N2}%");
            
            // Обновляем общие результаты
            result.UpMaxPercentage = upMaxPercentage;
            result.ShortfallTotal = shortfallTotal;
            result.ExcessTotal = excessTotal;
            
            // Создание графика влияния на бюджет
            await CreateBudgetImpactGraphAsync(result, geokName);
            
            _logger.LogInformation("Ребалансировка бюджета завершена");
            
            return result;
        }
        
        private async Task<T> LoadDataAsync<T>(string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            _logger.LogInformation($"Загрузка данных из: {filePath}");
            
            if (!File.Exists(filePath))
            {
                _logger.LogError($"Файл не найден: {filePath}");
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }
            
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }
        
        private double FindBudgetNeutralUpMax(SimulationData[] data, string budgetNewField, double shortfallTotal, double initialGuess)
        {
            // Функция для поиска корня уравнения
            Func<double, double> equation = upmax =>
            {
                double sum = 0;
                foreach (var item in data)
                {
                    double budgetOld = item.ЦСМ_ГСВ_2023 * 1000;
                    double budgetNew = item.GetNewBudget(budgetNewField);
                    double excess = budgetNew <= (1 + upmax) * budgetOld ? 0 : budgetNew - (1 + upmax) * budgetOld;
                    
                    sum += excess;
                }
                
                return sum - shortfallTotal;
            };
            
            // Используем алгоритм Ньютона-Рафсона для поиска корня
            double upMaxValue = Brent.FindRoot(equation, 0, 2 * initialGuess, 1e-6);
            
            return upMaxValue;
        }
        
        private async Task CreateBudgetImpactGraphAsync(RebalancingResult result, string geokName)
        {
            _logger.LogInformation($"Создание графика влияния на бюджет для {geokName} с ребалансировкой");
            
            var plt = new ScottPlot.Plot(1200, 1800);
            
            // Сортировка данных по региону и названию
            var sortedData = result.Data.Values
                .OrderBy(x => x.Region)
                .ThenBy(x => x.NewName)
                .ToArray();
            
            // Получение уникальных регионов для легенды
            var uniqueRegions = sortedData.Select(w => w.Region).Distinct().ToArray();
            
            // Группируем данные по региону
            var groupedData = sortedData.GroupBy(w => w.Region);
            
            // Для каждого региона создаем отдельные столбцы
            foreach (var group in groupedData)
            {
                var color = GetColorForRegion(group.Key);
                var regionItems = group.ToArray();
                
                for (int i = 0; i < regionItems.Length; i++)
                {
                    // Найдем индекс элемента в общем массиве
                    int index = Array.IndexOf(sortedData, regionItems[i]);
                    if (index >= 0)
                    {
                        double[] barPositions = new double[] { index };
                        double[] barValues = new double[] { regionItems[i].ImpactAdj };
                        var bar = plt.AddBar(barValues, barPositions);
                        bar.Color = color;
                    }
                }
                
                // Добавляем элемент в легенду
                plt.Legend(true);
            }
            
            // Настройка осей
            double[] positions = Enumerable.Range(0, sortedData.Length).Select(i => (double)i).ToArray();
            plt.XAxis.ManualTickPositions(positions, sortedData.Select(w => w.NewName).ToArray());
            plt.XAxis.TickLabelStyle(rotation: 45, fontSize: 8);
            plt.XAxis.Label("Медицинские организации");
            
            plt.YAxis.Label("% от первоначального бюджета");
            plt.SetAxisLimits(yMin: -20, yMax: 20);
            
            // Добавление вертикальных линий
            plt.AddVerticalLine(0, System.Drawing.Color.Black, 1, LineStyle.Solid);
            plt.AddVerticalLine(-result.DownMaxPercentage, System.Drawing.Color.Red, 1, LineStyle.Solid);
            plt.AddVerticalLine(result.UpMaxPercentage, System.Drawing.Color.Green, 1, LineStyle.Solid);
            
            // Добавление заголовка
            plt.Title($"Влияние половозрастнои корректировки и {geokName} на бюджет оз с ребалансировкой");
            
            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, $"Fig8.budget impact of sex-age adjustment with {geokName} and rebalancing.png");
            plt.SaveFig(outputPath);
            
            _logger.LogInformation($"График влияния на бюджет сохранен в: {outputPath}");
        }
        
        private System.Drawing.Color GetColorForRegion(string region)
        {
            return _regionColors.ContainsKey(region) 
                ? _regionColors[region] 
                : System.Drawing.Color.Gray; // Цвет по умолчанию
        }
    }
    
    public class RebalancingResult
    {
        public double TotalBudget { get; set; }
        public double DownMaxPercentage { get; set; }
        public double UpMaxPercentage { get; set; }
        public double ShortfallTotal { get; set; }
        public double ExcessTotal { get; set; }
        public Dictionary<int, RebalancedBudgetData> Data { get; set; } = new Dictionary<int, RebalancedBudgetData>();
    }
    
    public class RebalancedBudgetData
    {
        public int Scm_NewCode { get; set; }
        public string Region { get; set; } = string.Empty;
        public string NewName { get; set; } = string.Empty;
        public double People { get; set; }
        public double BudgetOld { get; set; }
        public double BudgetNew { get; set; }
        public double Impact { get; set; }
        public double Shortfall { get; set; }
        public double Excess { get; set; }
        public double BudgetNewAdj { get; set; }
        public double ImpactAdj { get; set; }
    }
}