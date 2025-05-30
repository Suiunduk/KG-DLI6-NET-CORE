using System.Text.Json;
using KG_DLI6_NET_CORE.Models;
using MathNet.Numerics;
using MathNet.Numerics.RootFinding;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;

namespace KG_DLI6_NET_CORE.Services
{
    public class BudgetRebalancingService
    {
        private readonly ILogger<BudgetRebalancingService> _logger;
        private readonly string _workingPath;
        private readonly string _outputPath;
        
        // Словарь цветов для областей
        private readonly Dictionary<string, OxyColor> _regionColors = new()
        {
            { "Баткенская обл.", OxyColors.Red },
            { "Бишкек г.", OxyColors.Blue },
            { "Чуйская обл.", OxyColors.Green },
            { "Иссык-Кульская обл.", OxyColors.Black },
            { "Джалал-Абадская обл.", OxyColors.Magenta },
            { "Нарынская обл.", OxyColors.Green },
            { "Ошская обл.", OxyColors.Yellow },
            { "Таласская обл.", OxyColors.Cyan },
            { "Ош г.", OxyColors.Black }
        };

        public BudgetRebalancingService(ILogger<BudgetRebalancingService> logger)
        {
            _logger = logger;
            
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
            
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
            await CreateBudgetImpactGraphAsync(result.Data.Values.ToList(), geokName);
            
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
            _logger.LogInformation($"Поиск бюджетно-нейтрального upmax. Начальное значение: {initialGuess}, Общая недостача: {shortfallTotal}");

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

            try
            {
                // Проверяем значения на границах
                double lowerValue = equation(0);
                double upperValue = equation(2 * initialGuess);
                
                _logger.LogInformation($"Значение функции при upmax=0: {lowerValue}");
                _logger.LogInformation($"Значение функции при upmax={2 * initialGuess}: {upperValue}");

                // Если знаки разные, используем метод Брента
                if (Math.Sign(lowerValue) != Math.Sign(upperValue))
                {
                    return Brent.FindRoot(equation, 0, 2 * initialGuess, 1e-6, 100);
                }

                // Если знаки одинаковые, используем метод бисекции с расширением интервала
                double lower = 0;
                double upper = 2 * initialGuess;
                int maxAttempts = 10;

                while (Math.Sign(equation(lower)) == Math.Sign(equation(upper)) && maxAttempts > 0)
                {
                    upper *= 2;
                    maxAttempts--;
                    _logger.LogInformation($"Расширение интервала поиска. Новая верхняя граница: {upper}");
                }

                if (maxAttempts == 0)
                {
                    _logger.LogWarning("Не удалось найти интервал с разными знаками. Возвращаем начальное значение.");
                    return initialGuess;
                }

                // Теперь используем метод Брента с расширенным интервалом
                return Brent.FindRoot(equation, lower, upper, 1e-6, 100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске бюджетно-нейтрального upmax");
                
                // В случае ошибки возвращаем приближенное значение на основе линейной интерполяции
                double upmax = 0;
                double step = 0.1;
                double bestDiff = double.MaxValue;
                double bestUpmax = initialGuess;

                while (upmax <= 2 * initialGuess)
                {
                    double currentDiff = Math.Abs(equation(upmax));
                    if (currentDiff < bestDiff)
                    {
                        bestDiff = currentDiff;
                        bestUpmax = upmax;
                    }
                    upmax += step;
                }

                _logger.LogInformation($"Использовано приближенное значение upmax: {bestUpmax}");
                return bestUpmax;
            }
        }
        
        private async Task CreateBudgetImpactGraphAsync(List<RebalancedBudgetData> results, string geokName)
        {
            _logger.LogInformation("Создание графика влияния на бюджет для {GeokName}...", geokName);
            
            var model = new PlotModel
            {
                Title = $"Влияние на бюджет ({geokName})",
                TitleFontSize = 16,
                TitleFontWeight = FontWeights.Bold,
                PlotAreaBorderThickness = new OxyThickness(1),
                PlotAreaBorderColor = OxyColors.Black,
                Background = OxyColors.White,
                TextColor = OxyColors.Black
            };

            // Настройка осей
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Отклонение бюджета (%)",
                TitleFontSize = 14,
                TitleFontWeight = FontWeights.Bold,
                AxisTitleDistance = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColors.LightGray,
                Minimum = -100,
                Maximum = 100,
                MajorStep = 20,
                MinorStep = 5,
                TickStyle = TickStyle.Outside,
                AxislineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.Black,
                ExtraGridlines = new double[] { 0 },
                ExtraGridlineStyle = LineStyle.Solid,
                ExtraGridlineColor = OxyColors.Black,
                ExtraGridlineThickness = 1
            };

            var yAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Медицинские организации",
                TitleFontSize = 14,
                TitleFontWeight = FontWeights.Bold,
                AxisTitleDistance = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColors.LightGray,
                TickStyle = TickStyle.Outside,
                AxislineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.Black
            };

            model.Axes.Add(xAxis);
            model.Axes.Add(yAxis);

            // Создание серии
            var series = new BarSeries
            {
                Title = "Отклонения",
                FillColor = OxyColors.SteelBlue,
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                BarWidth = 1
            };

            // Добавление данных
            foreach (var item in results.OrderBy(d => d.NewName))
            {
                series.Items.Add(new BarItem(item.ImpactAdj, 1));
                yAxis.Labels.Add(item.NewName);
            }

            model.Series.Add(series);

            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, $"budget_impact_{geokName}.png");
            using (var stream = File.Create(outputPath))
            {
                var pngExporter = new OxyPlot.ImageSharp.PngExporter(1200, 1800);
                pngExporter.Export(model, stream);
            }

            _logger.LogInformation($"График влияния на бюджет сохранен в: {outputPath}");
        }
        
        private OxyColor GetColorForRegion(string region)
        {
            return _regionColors.TryGetValue(region, out var color) ? color : OxyColors.Gray;
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