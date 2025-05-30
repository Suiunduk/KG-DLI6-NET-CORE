using System.Text.Json;
using System.Text.Json.Serialization;
using KG_DLI6_NET_CORE.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;
using OxyPlot.ImageSharp;
using OxyPlot.Legends;

namespace KG_DLI6_NET_CORE.Services
{
    public class BudgetSimulationService
    {
        private readonly ILogger<BudgetSimulationService> _logger;
        private readonly string _workingPath;
        private readonly string _outputPath;
        
        // Процент переназначения бюджета для ОУЗ без узких специалистов
        private readonly double _reassignPercentage = 0.25; // 25%
        
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

        public BudgetSimulationService(ILogger<BudgetSimulationService> logger)
        {
            _logger = logger;
            
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
            
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
            //await CreateScatterPlotAsync(data, geokName);
            
            // 2. Расчет скорректированного подушевого норматива и базового бюджета
            await CalculateAdjustedRatesAndBudgetsAsync(data, geokName);
            
            // 3. Графическое представление влияния на бюджет
            //await CreateBudgetImpactGraphAsync(data, geokName);
            
            // 4. Создание гистограммы распределения
            await CreateHistogramAsync(data, geokName);
        }
        
        private async Task CreateScatterPlotAsync(Dictionary<int, SimulationData> data, string geokName)
        {
            try
            {
                _logger.LogInformation($"Начало создания диаграммы рассеяния для {geokName}");
                
                if (data == null || !data.Any())
                {
                    _logger.LogError("Данные для построения графика отсутствуют");
                    return;
                }

                _logger.LogInformation($"Количество записей для обработки: {data.Count}");
                
                var model = new PlotModel
                {
                    Title = $"Точечная диаграмма: geok_old против {geokName}\nScatter Plot: geok_old vs {geokName}",
                    TitleFontSize = 14,
                    TitleFontWeight = FontWeights.Bold,
                    PlotAreaBorderThickness = new OxyThickness(1),
                    PlotAreaBorderColor = OxyColors.Black,
                    Background = OxyColors.White,
                    TextColor = OxyColors.Black
                };

                _logger.LogInformation("Настройка осей графика");
                
                // Настройка осей
                var xAxis = new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "geok_old",
                    TitleFontSize = 12,
                    TitleFontWeight = FontWeights.Bold,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    MinorGridlineStyle = LineStyle.Dot,
                    MinorGridlineColor = OxyColors.LightGray,
                    MinimumPadding = 0.1,
                    MaximumPadding = 0.1
                };
                
                var yAxis = new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = geokName,
                    TitleFontSize = 12,
                    TitleFontWeight = FontWeights.Bold,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    MinorGridlineStyle = LineStyle.Dot,
                    MinorGridlineColor = OxyColors.LightGray,
                    MinimumPadding = 0.1,
                    MaximumPadding = 0.1
                };
                
                model.Axes.Add(xAxis);
                model.Axes.Add(yAxis);

                _logger.LogInformation("Группировка данных по регионам");
                
                // Группировка данных по регионам
                var groupedData = data.Values
                    .Where(d => !string.IsNullOrEmpty(d.Region))
                    .GroupBy(d => d.Region)
                    .ToList();

                _logger.LogInformation($"Найдено {groupedData.Count} регионов для обработки");
                
                int totalPoints = 0;
                // Добавление точек по регионам
                foreach (var group in groupedData)
                {
                    _logger.LogInformation($"Обработка региона: {group.Key}");
                    
                    var color = GetColorForRegion(group.Key);
                    var scatterSeries = new ScatterSeries
                    {
                        Title = group.Key,
                        MarkerType = MarkerType.Circle,
                        MarkerSize = 4,
                        MarkerFill = color
                    };
                    
                    foreach (var item in group)
                    {
                        var x = item.geok_old;
                        var y = GetGeokValue(item, geokName);
                        if (!double.IsNaN(x) && !double.IsNaN(y) && x != 0 && y != 0)
                        {
                            scatterSeries.Points.Add(new ScatterPoint(x, y));
                            totalPoints++;
                        }
                    }
                    
                    if (scatterSeries.Points.Count > 0)
                    {
                        model.Series.Add(scatterSeries);
                        _logger.LogInformation($"Добавлено {scatterSeries.Points.Count} точек для региона {group.Key}");
                    }
                }

                _logger.LogInformation($"Всего добавлено {totalPoints} точек на график");
                
                if (totalPoints == 0)
                {
                    _logger.LogError("Нет данных для отображения на графике");
                    return;
                }

                // Добавление диагональной линии
                model.Annotations.Add(new LineAnnotation
                {
                    Type = LineAnnotationType.LinearEquation,
                    Slope = 1,
                    Intercept = 0,
                    Color = OxyColors.Red,
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 1
                });
                
                // Настройка легенды
                var legend = new Legend
                {
                    LegendPlacement = LegendPlacement.Outside,
                    LegendPosition = LegendPosition.RightTop,
                    LegendOrientation = LegendOrientation.Vertical,
                    LegendBorder = OxyColors.Black,
                    LegendBackground = OxyColor.FromAColor(200, OxyColors.White)
                };
                model.Legends.Add(legend);
                
                // Настройка отображения
                model.PlotMargins = new OxyThickness(60, 40, 120, 40);

                _logger.LogInformation("Подготовка к сохранению графика");
                
                // Проверяем и создаем директорию, если она не существует
                if (!Directory.Exists(_outputPath))
                {
                    _logger.LogInformation($"Создание директории: {_outputPath}");
                    Directory.CreateDirectory(_outputPath);
                }
                
                // Сохранение графика
                var outputPath = Path.Combine(_outputPath, $"scatter_{geokName}.png");
                
                // Проверяем, не занят ли файл
                if (File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                        _logger.LogInformation("Удален существующий файл графика");
                    }
                    catch (IOException ex)
                    {
                        _logger.LogError(ex, "Не удалось удалить существующий файл графика");
                        outputPath = Path.Combine(_outputPath, $"scatter_{geokName}_{DateTime.Now:yyyyMMddHHmmss}.png");
                        _logger.LogInformation($"Используется альтернативное имя файла: {outputPath}");
                    }
                }

                try
                {
                    _logger.LogInformation("Начало экспорта графика");
                    using (var stream = File.Create(outputPath))
                    {
                        var pngExporter = new OxyPlot.ImageSharp.PngExporter(800, 600);
                        pngExporter.Export(model, stream);
                    }
                    _logger.LogInformation($"График успешно сохранен в: {outputPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при сохранении графика");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при создании диаграммы рассеяния для {geokName}");
                throw;
            }
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
            try
            {
                _logger.LogInformation($"Начало создания графика влияния на бюджет для {geokName}");
                
                if (data == null || !data.Any())
                {
                    _logger.LogError("Данные для построения графика отсутствуют");
                    return;
                }

                var model = new PlotModel
                {
                    Title = $"Влияние половозрастной корректировки и {geokName} на бюджет ОЗ\nImpact of sex-age adjustment and {geokName} on HCO budget",
                    TitleFontSize = 14,
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
                    Title = "% от первоначального бюджета / % of original budget",
                    TitleFontSize = 12,
                    TitleFontWeight = FontWeights.Bold,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    MinorGridlineStyle = LineStyle.Dot,
                    MinorGridlineColor = OxyColors.LightGray,
                    Minimum = -40,
                    Maximum = 40,
                    MajorStep = 10,
                    MinorStep = 5
                };

                var yAxis = new CategoryAxis
                {
                    Position = AxisPosition.Left,
                    TitleFontSize = 12,
                    TitleFontWeight = FontWeights.Bold,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    MinorGridlineStyle = LineStyle.Dot,
                    MinorGridlineColor = OxyColors.LightGray,
                    GapWidth = 0.1,
                    IsTickCentered = false,
                    MinimumPadding = 0.1,
                    MaximumPadding = 0.1
                };

                model.Axes.Add(xAxis);
                model.Axes.Add(yAxis);

                // Группировка и сортировка данных
                var sortedData = data.Values
                    .OrderBy(d => GetImpact(d, $"impact_{geokName}"))
                    .ToList();

                // Добавление данных
                int categoryIndex = 0;
                foreach (var item in sortedData)
                {
                    var color = GetColorForRegion(item.Region);
                    var series = new BarSeries
                    {
                        Title = item.Region,
                        FillColor = color,
                        StrokeColor = OxyColors.Black,
                        StrokeThickness = 1,
                        BarWidth = 0.8
                    };
                    
                    var impact = GetImpact(item, $"impact_{geokName}");
                    if (!double.IsNaN(impact))
                    {
                        series.Items.Add(new BarItem { Value = impact, CategoryIndex = categoryIndex });
                        yAxis.Labels.Add(item.NewName);
                        categoryIndex++;
                    }
                    
                    if (series.Items.Count > 0 && !model.Series.Any(s => s.Title == item.Region))
                    {
                        model.Series.Add(series);
                    }
                }

                // Добавление вертикальной линии на нуле
                model.Annotations.Add(new LineAnnotation
                {
                    Type = LineAnnotationType.Vertical,
                    X = 0,
                    Color = OxyColors.Black,
                    LineStyle = LineStyle.Solid,
                    StrokeThickness = 1
                });

                // Настройка легенды
                var legend = new Legend
                {
                    LegendPlacement = LegendPlacement.Outside,
                    LegendPosition = LegendPosition.RightTop,
                    LegendOrientation = LegendOrientation.Vertical,
                    LegendBorder = OxyColors.Black,
                    LegendBackground = OxyColor.FromAColor(200, OxyColors.White),
                    LegendTitle = "Области и города / Regions"
                };
                model.Legends.Add(legend);

                // Настройка отображения
                model.PlotMargins = new OxyThickness(120, 40, 120, 40);
                yAxis.IsZoomEnabled = false;
                yAxis.IsPanEnabled = false;

                _logger.LogInformation("Подготовка к сохранению графика");
                
                // Проверяем и создаем директорию, если она не существует
                if (!Directory.Exists(_outputPath))
                {
                    _logger.LogInformation($"Создание директории: {_outputPath}");
                    Directory.CreateDirectory(_outputPath);
                }
                
                // Сохранение графика
                var outputPath = Path.Combine(_outputPath, $"budget_impact_{geokName}.png");
                
                // Проверяем, не занят ли файл
                if (File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                        _logger.LogInformation("Удален существующий файл графика");
                    }
                    catch (IOException ex)
                    {
                        _logger.LogError(ex, "Не удалось удалить существующий файл графика");
                        outputPath = Path.Combine(_outputPath, $"budget_impact_{geokName}_{DateTime.Now:yyyyMMddHHmmss}.png");
                        _logger.LogInformation($"Используется альтернативное имя файла: {outputPath}");
                    }
                }

                try
                {
                    _logger.LogInformation("Начало экспорта графика");
                    using (var stream = File.Create(outputPath))
                    {
                        var pngExporter = new OxyPlot.ImageSharp.PngExporter(800, 1200);
                        pngExporter.Export(model, stream);
                    }
                    _logger.LogInformation($"График успешно сохранен в: {outputPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при сохранении графика");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при создании графика влияния на бюджет для {geokName}");
                throw;
            }
        }
        
        private async Task CreateHistogramAsync(Dictionary<int, SimulationData> data, string geokName)
        {
            try
            {
                _logger.LogInformation($"Начало создания гистограммы для {geokName}");
                
                if (data == null || !data.Any())
                {
                    _logger.LogError("Данные для построения гистограммы отсутствуют");
                    return;
                }

                var model = new PlotModel
                {
                    Title = $"Распределение влияния на бюджет ({geokName})\nDistribution of budget impact ({geokName})",
                    TitleFontSize = 14,
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
                    Title = "% бюджета 2023 г. / % of 2023 budget",
                    TitleFontSize = 12,
                    TitleFontWeight = FontWeights.Bold,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    MinorGridlineStyle = LineStyle.Dot,
                    MinorGridlineColor = OxyColors.LightGray,
                    MinimumPadding = 0.1,
                    MaximumPadding = 0.1
                };

                var yAxis = new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = "Количество МО / Number of HCOs",
                    TitleFontSize = 12,
                    TitleFontWeight = FontWeights.Bold,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColors.LightGray,
                    MinorGridlineStyle = LineStyle.Dot,
                    MinorGridlineColor = OxyColors.LightGray,
                    Minimum = 0,
                    MinimumPadding = 0.1,
                    MaximumPadding = 0.1
                };

                model.Axes.Add(xAxis);
                model.Axes.Add(yAxis);

                // Получаем значения влияния
                var values = data.Values
                    .Select(d => GetImpact(d, $"impact_{geokName}"))
                    .Where(v => !double.IsNaN(v))
                    .ToArray();
                
                if (values.Length > 0)
                {
                    // Вычисляем границы и количество бинов
                    var min = values.Min();
                    var max = values.Max();
                    const double binWidth = 2.0; // 2% интервалы
                    var numBins = (int)((max - min) / binWidth) + 1;

                    // Создаем биннинг
                    var bins = new int[numBins];
                    foreach (var value in values)
                    {
                        var binIndex = (int)((value - min) / binWidth);
                        if (binIndex >= 0 && binIndex < numBins)
                        {
                            bins[binIndex]++;
                        }
                    }

                    // Создание гистограммы
                    var histogram = new RectangleBarSeries
                    {
                        FillColor = OxyColors.RoyalBlue,
                        StrokeColor = OxyColors.Black,
                        StrokeThickness = 1
                    };

                    // Добавление баров гистограммы
                    for (int i = 0; i < numBins; i++)
                    {
                        var x0 = min + i * binWidth;
                        var x1 = x0 + binWidth;
                        histogram.Items.Add(new RectangleBarItem(x0, 0, x1, bins[i]));
                    }

                    model.Series.Add(histogram);

                    // Добавление вертикальной линии на нуле
                    model.Annotations.Add(new LineAnnotation
                    {
                        Type = LineAnnotationType.Vertical,
                        X = 0,
                        Color = OxyColors.Black,
                        LineStyle = LineStyle.Dash,
                        StrokeThickness = 1
                    });

                    // Автоматическое определение пределов осей
                    xAxis.Minimum = min - binWidth;
                    xAxis.Maximum = max + binWidth;
                    xAxis.MajorStep = 5;
                    xAxis.MinorStep = 1;
                    
                    yAxis.Maximum = bins.Max() + 1;
                    yAxis.MajorStep = 1;
                    yAxis.MinorStep = 0.5;
                }

                // Настройка отображения
                model.PlotMargins = new OxyThickness(60, 40, 20, 40);

                _logger.LogInformation("Подготовка к сохранению гистограммы");
                
                // Проверяем и создаем директорию, если она не существует
                if (!Directory.Exists(_outputPath))
                {
                    _logger.LogInformation($"Создание директории: {_outputPath}");
                    Directory.CreateDirectory(_outputPath);
                }
                
                // Сохранение графика
                var outputPath = Path.Combine(_outputPath, $"histogram_{geokName}.png");
                
                // Проверяем, не занят ли файл
                if (File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                        _logger.LogInformation("Удален существующий файл гистограммы");
                    }
                    catch (IOException ex)
                    {
                        _logger.LogError(ex, "Не удалось удалить существующий файл гистограммы");
                        outputPath = Path.Combine(_outputPath, $"histogram_{geokName}_{DateTime.Now:yyyyMMddHHmmss}.png");
                        _logger.LogInformation($"Используется альтернативное имя файла: {outputPath}");
                    }
                }

                try
                {
                    _logger.LogInformation("Начало экспорта гистограммы");
                    using (var stream = File.Create(outputPath))
                    {
                        var pngExporter = new OxyPlot.ImageSharp.PngExporter(800, 600);
                        pngExporter.Export(model, stream);
                    }
                    _logger.LogInformation($"Гистограмма успешно сохранена в: {outputPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при сохранении гистограммы");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при создании гистограммы для {geokName}");
                throw;
            }
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
        
        private OxyColor GetColorForRegion(string region)
        {
            return _regionColors.TryGetValue(region, out var color) ? color : OxyColors.Gray;
        }
    }
    
    public class SimulationResult
    {
        public SimulationData HcoData { get; set; }
        public double ImpactOld { get; set; }
        public double Impact1 { get; set; }
    }
}