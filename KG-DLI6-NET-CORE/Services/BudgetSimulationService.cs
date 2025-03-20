using System.Text.Json;
using System.Text.Json.Serialization;
using KG_DLI6_NET_CORE.Models;
using ScottPlot;

namespace KG_DLI6_NET_CORE.Services
{
    public class BudgetSimulationService
    {
        private readonly ILogger<BudgetSimulationService> _logger;
        private readonly string _workingPath;
        private readonly string _outputPath;
        
        // Процент переназначения бюджета для ОУЗ без узких специалистов
        private readonly double _reassignPercentage = 0.25; // 25%
        
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

        public BudgetSimulationService(ILogger<BudgetSimulationService> logger)
        {
            _logger = logger;
            
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
            
            // Создаем директории, если они не существуют
            Directory.CreateDirectory(_workingPath);
            Directory.CreateDirectory(_outputPath);
        }
        
        public async Task<Dictionary<int, SimulationResult>> SimulateBudgetsAsync()
        {
            _logger.LogInformation("Начало симуляции бюджетов");
            
            // Загрузка данных
            var mergedData = await LoadDataAsync<Dictionary<int, MergedHcoData>>("merged_df");
            
            _logger.LogInformation($"Загружены объединенные данные: {mergedData.Count} записей");
            
            // Преобразование в рабочий формат
            var simulationData = ConvertToSimulationData(mergedData);
            
            // Корректировка рабочей нагрузки для железнодорожной клиники
            AdjustWorkloadForRailwayClinic(simulationData);
            
            // Определение всех географических коэффициентов для симуляции
            DefineGeographicCoefficients(simulationData);
            
            // Создание списка коэффициентов для симуляции
            var geokList = new List<string> { "old", "1" };
            
            // Запуск цикла симуляции для каждого коэффициента
            foreach (var geok in geokList)
            {
                await RunSimulationForCoefficient(simulationData, geok);
            }
            
            // Сохранение результатов
            await SaveDataAsync(simulationData, "simulated_budgets");
            
            _logger.LogInformation("Симуляция бюджетов завершена");
            
            return simulationData.ToDictionary(
                d => d.Key, 
                d => new SimulationResult 
                { 
                    HcoData = d.Value,
                    ImpactOld = d.Value.impact_geok_old,
                    Impact1 = d.Value.impact_geok_1
                });
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
        
        private Dictionary<int, SimulationData> ConvertToSimulationData(Dictionary<int, MergedHcoData> mergedData)
        {
            var result = new Dictionary<int, SimulationData>();
            
            foreach (var item in mergedData)
            {
                var simData = new SimulationData
                {
                    Scm_NewCode = item.Value.Scm_NewCode,
                    Region = item.Value.Region,
                    NewName = item.Value.NewName,
                    FullName = item.Value.FullName,
                    people = item.Value.People,
                    geok_old = item.Value.geok_old ?? 0,
                    adjworkloadk = item.Value.adjworkloadk,
                    altitude = item.Value.altitude,
                    rural = item.Value.rural,
                    density = item.Value.density,
                    ЦСМ_ГСВ_2023 = item.Value.ЦСМ_ГСВ_2023 ?? 0,
                    adj_originHO1 = item.Value.adj_originHO1,
                    adj_originHO2 = item.Value.adj_originHO2,
                    adj_destination = item.Value.adj_destination
                };
                
                result[item.Key] = simData;
            }
            
            return result;
        }
        
        private void AdjustWorkloadForRailwayClinic(Dictionary<int, SimulationData> data)
        {
            _logger.LogInformation("Корректировка рабочей нагрузки для железнодорожной клиники");
            
            // Особый случай - железнодорожная клиника
            if (data.ContainsKey(102272))
            {
                data[102272].adjworkloadk *= (1 - _reassignPercentage);
                _logger.LogInformation($"Рабочая нагрузка для железнодорожной клиники скорректирована: {data[102272].adjworkloadk}");
            }
        }
        
        private void DefineGeographicCoefficients(Dictionary<int, SimulationData> data)
        {
            _logger.LogInformation("Определение географических коэффициентов для симуляции");
            
            foreach (var item in data.Values)
            {
                // geok_1 = altitude + rural - 1
                item.geok_1 = item.altitude + item.rural - 1;
                
                // geok_2 = altitude
                item.geok_2 = item.altitude;
                
                // geok_3 = 1.3 if density < 57 else 1
                item.geok_3 = item.density < 57 ? 1.3 : 1.0;
            }
        }
        
        private async Task RunSimulationForCoefficient(Dictionary<int, SimulationData> data, string geokType)
        {
            _logger.LogInformation($"Запуск симуляции для коэффициента: {geokType}");
            
            string geokName = $"geok_{geokType}";
            
            // 1. Создание диаграммы рассеяния для сравнения коэффициентов
            await CreateScatterPlotAsync(data, geokName);
            
            // 2. Расчет скорректированного подушевого норматива и базового бюджета
            await CalculateAdjustedRatesAndBudgetsAsync(data, geokName);
            
            // 3. Графическое представление влияния на бюджет
            await CreateBudgetImpactGraphAsync(data, geokName);
        }
        
        private async Task CreateScatterPlotAsync(Dictionary<int, SimulationData> data, string geokName)
        {
            _logger.LogInformation($"Создание диаграммы рассеяния для {geokName}");
            
            var plt = new ScottPlot.Plot(1000, 800);
            
            // Получение значений для построения графика
            double[] xValues = data.Values.Select(d => d.geok_old).ToArray();
            double[] yValues = data.Values.Select(d => GetGeokValue(d, geokName)).ToArray();
            
            // Построение диаграммы рассеяния
            plt.AddScatter(xValues, yValues);
            
            // Настройка осей
            plt.XAxis.Label("geok_old");
            plt.YAxis.Label(geokName);
            
            // Установка одинаковых границ для осей X и Y
            double minValue = Math.Min(xValues.Min(), yValues.Min());
            double maxValue = Math.Max(xValues.Max(), yValues.Max());
            plt.SetAxisLimits(minValue, maxValue, minValue, maxValue);
            
            // Добавление диагональной линии
            var line = plt.AddLine(minValue, minValue, maxValue, maxValue);
            line.Color = System.Drawing.Color.Red;
            line.LineStyle = LineStyle.Dash;
            
            // Добавление заголовка
            plt.Title($"Scatter Plot: geok_old vs {geokName}");
            
            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, $"Fig6.{geokName}-Geok_old vs {geokName}.png");
            plt.SaveFig(outputPath);
            
            _logger.LogInformation($"Диаграмма рассеяния сохранена в: {outputPath}");
        }
        
        private async Task CalculateAdjustedRatesAndBudgetsAsync(Dictionary<int, SimulationData> data, string geokName)
        {
            _logger.LogInformation($"Расчет скорректированного подушевого норматива и бюджетов для {geokName}");
            
            string rawBudgetField = $"budget_raw_{geokName}";
            string add1Field = $"correction_add1_{geokName}";
            string add2Field = $"correction_add2_{geokName}";
            string subtrField = $"correction_subtr_{geokName}";
            string budgetNewField = $"budget_new_{geokName}";
            
            // Общий бюджет для распределения
            double totalBudget = data.Values.Sum(d => d.ЦСМ_ГСВ_2023) * 1000;
            
            // Расчет подушевого норматива
            double totalWeightedPopulation = data.Values.Sum(d => d.people * GetGeokValue(d, geokName) * d.adjworkloadk);
            double perCapitaRate = totalBudget / totalWeightedPopulation;
            
            _logger.LogInformation($"Общий бюджет: {totalBudget}");
            _logger.LogInformation($"Подушевой норматив: {perCapitaRate:F2} сомов");
            
            // Расчет базового бюджета для каждой организации
            foreach (var item in data.Values)
            {
                double geokValue = GetGeokValue(item, geokName);
                item.SetRawBudget(rawBudgetField, item.people * perCapitaRate * geokValue * item.adjworkloadk);
            }
            
            // Расчет корректировок для узких специалистов
            // 1. Добавление бюджета для первого референтного ЦСМ
            foreach (var item in data.Values)
            {
                if (item.adj_originHO1 > 0)
                {
                    var originHO = data.Values.FirstOrDefault(d => d.Scm_NewCode == (int)item.adj_originHO1);
                    if (originHO != null)
                    {
                        double rawBudget = GetRawBudget(originHO, rawBudgetField);
                        double add1Value = rawBudget * _reassignPercentage;
                        item.SetCorrectionAdd1(add1Field, add1Value);
                    }
                }
                else
                {
                    item.SetCorrectionAdd1(add1Field, 0);
                }
            }
            
            // 2. Добавление бюджета для второго референтного ЦСМ
            foreach (var item in data.Values)
            {
                if (item.adj_originHO2 > 0)
                {
                    var originHO = data.Values.FirstOrDefault(d => d.Scm_NewCode == (int)item.adj_originHO2);
                    if (originHO != null)
                    {
                        double rawBudget = GetRawBudget(originHO, rawBudgetField);
                        double add2Value = rawBudget * _reassignPercentage;
                        item.SetCorrectionAdd2(add2Field, add2Value);
                    }
                }
                else
                {
                    item.SetCorrectionAdd2(add2Field, 0);
                }
            }
            
            // 3. Вычитание бюджета из ГСВ, не имеющих узких специалистов
            foreach (var item in data.Values)
            {
                double rawBudget = GetRawBudget(item, rawBudgetField);
                double subtrValue = item.adj_destination > 0 ? rawBudget * _reassignPercentage : 0;
                item.SetCorrectionSubtr(subtrField, subtrValue);
            }
            
            // 4. Расчет нового бюджета для каждой организации
            foreach (var item in data.Values)
            {
                double rawBudget = GetRawBudget(item, rawBudgetField);
                double add1 = GetCorrectionAdd1(item, add1Field);
                double add2 = GetCorrectionAdd2(item, add2Field);
                double subtr = GetCorrectionSubtr(item, subtrField);
                
                double newBudget = rawBudget - subtr + add1 + add2;
                item.SetNewBudget(budgetNewField, newBudget);
            }
            
            // Проверка расчетов
            double totalNewBudget = data.Values.Sum(d => GetNewBudget(d, budgetNewField));
            
            _logger.LogInformation($"Корректировка для ЦСМ с дополнительным населением: {perCapitaRate * _reassignPercentage:F2} сомов на человека");
            _logger.LogInformation($"Корректировка для ЦСМ без узких специалистов: {perCapitaRate * _reassignPercentage * (-1):F2} сомов на человека");
            _logger.LogInformation($"Распределенный бюджет: {totalNewBudget:F2}");
            
            // 5. Расчет влияния на бюджет
            string impactField = $"impact_{geokName}";
            foreach (var item in data.Values)
            {
                double newBudget = GetNewBudget(item, budgetNewField);
                double oldBudget = item.ЦСМ_ГСВ_2023 * 1000;
                double impact = ((newBudget / oldBudget) - 1) * 100;
                item.SetImpact(impactField, impact);
            }
        }
        
        private async Task CreateBudgetImpactGraphAsync(Dictionary<int, SimulationData> data, string geokName)
        {
            _logger.LogInformation($"Создание графика влияния на бюджет для {geokName}");
            
            string impactField = $"impact_{geokName}";
            
            var plt = new ScottPlot.Plot(1200, 1800);
            
            // Сортировка данных по региону и названию
            var sortedData = data.Values
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
                        double[] barValues = new double[] { GetImpact(regionItems[i], impactField) };
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
            plt.SetAxisLimits(yMin: -40, yMax: 40);
            
            // Добавление вертикальной линии на значении 0
            plt.AddVerticalLine(0, System.Drawing.Color.Black, 1, LineStyle.Solid);
            
            // Добавление заголовка
            plt.Title($"Влияние половозрастнои корректировки и {geokName} на бюджет оз");
            
            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, $"Fig7.budget impact of sex-age adjustment with {geokName}.png");
            plt.SaveFig(outputPath);
            
            _logger.LogInformation($"График влияния на бюджет сохранен в: {outputPath}");
        }
        
        private async Task SaveDataAsync<T>(T data, string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            _logger.LogInformation($"Сохранение данных в: {filePath}");

            var options = new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(data, options);
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInformation("Данные успешно сохранены");
        }
        
        private double GetGeokValue(SimulationData data, string geokName)
        {
            return geokName switch
            {
                "geok_old" => data.geok_old,
                "geok_1" => data.geok_1,
                "geok_2" => data.geok_2,
                "geok_3" => data.geok_3,
                _ => 1.0 // По умолчанию
            };
        }
        
        private double GetRawBudget(SimulationData data, string fieldName)
        {
            return data.GetRawBudget(fieldName);
        }
        
        private double GetCorrectionAdd1(SimulationData data, string fieldName)
        {
            return data.GetCorrectionAdd1(fieldName);
        }
        
        private double GetCorrectionAdd2(SimulationData data, string fieldName)
        {
            return data.GetCorrectionAdd2(fieldName);
        }
        
        private double GetCorrectionSubtr(SimulationData data, string fieldName)
        {
            return data.GetCorrectionSubtr(fieldName);
        }
        
        private double GetNewBudget(SimulationData data, string fieldName)
        {
            return data.GetNewBudget(fieldName);
        }
        
        private double GetImpact(SimulationData data, string fieldName)
        {
            return data.GetImpact(fieldName);
        }
        
        private System.Drawing.Color GetColorForRegion(string region)
        {
            return _regionColors.ContainsKey(region) 
                ? _regionColors[region] 
                : System.Drawing.Color.Gray; // Цвет по умолчанию
        }
    }
    
    public class SimulationResult
    {
        public SimulationData HcoData { get; set; }
        public double ImpactOld { get; set; }
        public double Impact1 { get; set; }
    }
}