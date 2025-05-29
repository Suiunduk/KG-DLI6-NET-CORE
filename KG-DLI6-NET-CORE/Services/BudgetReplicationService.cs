using System.Text.Json;
using KG_DLI6_NET_CORE.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;

namespace KG_DLI6_NET_CORE.Services
{
    public class BudgetReplicationService
    {
        private readonly ILogger<BudgetReplicationService> _logger;
        private readonly string _workingPath;
        private readonly string _outputPath;
        
        // Константы из Python-кода
        private readonly double _source1 = 1206684977; // Бюджет для узких специалистов
        private readonly double _source2 = 3308552674; // Бюджет для семейной медицины
        private readonly double _insuredUninsuredRatio = 3.02; // Коэффициент соотношения застрахованных и незастрахованных
        
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

        public BudgetReplicationService(ILogger<BudgetReplicationService> logger)
        {
            _logger = logger;
            
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
            
            // Создаем директории, если они не существуют
            Directory.CreateDirectory(_workingPath);
            Directory.CreateDirectory(_outputPath);
        }
        
        public async Task<Dictionary<int, BudgetReplicationData>> ReplicateOldBudgetsAsync()
        {
            _logger.LogInformation("Начало воспроизведения старых бюджетов");
            
            // Загрузка данных
            var coeffsqu = await LoadDataAsync<Dictionary<int, Dictionary<string, double>>>("coeffsqu");
            var mergedData = await LoadDataAsync<Dictionary<int, MergedHcoData>>("merged_df");
            
            _logger.LogInformation($"Загружены объединенные данные: {mergedData.Count} записей");
            
            // Преобразование в рабочий формат
            var budgetData = ConvertToBudgetData(mergedData);
            
            // Корректировка для организаций без узких специалистов
            AdjustForNarrowSpecialists(budgetData);
            
            // Расчет коэффициента предпочтения
            CalculatePreferentialCoefficient(budgetData);
            
            // Расчет бюджетов по методу 1 (репликация расчетов Excel)
            CalculateBudgetMethod1(budgetData);
            
            // Расчет бюджетов по методу 2 (скорректированный метод)
            CalculateBudgetMethod2(budgetData);
            
            // Расчет отклонений
            CalculateDeviations(budgetData);
            
            // Сохранение результатов
            await SaveDataAsync(budgetData, "oldHObudgets");
            
            // Создание графиков
            await CreateBudgetReplicationGraphsAsync(budgetData);
            
            _logger.LogInformation("Воспроизведение старых бюджетов завершено");
            
            return budgetData;
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
        
        private Dictionary<int, BudgetReplicationData> ConvertToBudgetData(Dictionary<int, MergedHcoData> mergedData)
        {
            var result = new Dictionary<int, BudgetReplicationData>();
            
            foreach (var item in mergedData)
            {
                var budgetItem = new BudgetReplicationData
                {
                    Scm_NewCode = item.Value.Scm_NewCode,
                    Region = item.Value.Region,
                    Raion = item.Value.Raion,
                    SoateRegion = item.Value.SoateRegion.ToString(),
                    Soate_Raion = item.Value.Soate_Raion.ToString(),
                    Scm_Code = item.Value.Scm_Code?.ToString() ?? "",
                    NewName = item.Value.NewName,
                    FullName = item.Value.FullName,
                    nr_mhi = item.Value.nr_mhi,
                    People = item.Value.People,
                    geok_old_ns = item.Value.geok_old_ns,
                    geok_old_gsv = item.Value.geok_old_gsv ?? 0,
                    geok_old = item.Value.geok_old ?? 0,
                    budget_2023 = item.Value.budget_2023 ?? 0,
                    ЦСМ_ГСВ_2023 = item.Value.ЦСМ_ГСВ_2023 ?? 0,
                    Все_нас_2023 = item.Value.Все_нас_2023 ?? 0,
                    застрах_нас_2023 = item.Value.nr_mhi, // Предположим, что застрахованное население равно nr_mhi
                    не_застрах_нас_2023 = item.Value.People - item.Value.nr_mhi // Предположим, что незастрахованное население - это разница
                };
                
                // Начальное значение - то же, что и обычное население
                budgetItem.people_NS = budgetItem.People;
                
                result[item.Key] = budgetItem;
            }
            
            return result;
        }
        
        private void AdjustForNarrowSpecialists(Dictionary<int, BudgetReplicationData> budgetData)
        {
            _logger.LogInformation("Корректировка для организаций без узких специалистов");
            
            // Сначала мы должны создать связи между организациями на основе источников коррекции
            // Это будет сложно воспроизвести без дополнительных данных о связях между организациями
            // В Python-коде используются колонки, которых у нас нет: источник_кор1, источник_кор2, назначение_кор
            
            // В этой реализации мы предполагаем, что данные об узких специалистах уже учтены
            // и people_NS равно People для всех организаций, кроме особых случаев
            
            // Особый случай - железнодорожная клиника не имеет приписанных для узких специалистов
            foreach (var item in budgetData.Values)
            {
                if (item.Scm_NewCode == 102272)
                {
                    item.people_NS = 0;
                }
            }
            
            _logger.LogInformation($"Население для узких специалистов: {budgetData.Values.Sum(d => d.people_NS)}");
            _logger.LogInformation($"Общее население: {budgetData.Values.Sum(d => d.People)}");
        }
        
        private void CalculatePreferentialCoefficient(Dictionary<int, BudgetReplicationData> budgetData)
        {
            _logger.LogInformation("Расчет коэффициента предпочтения");
            
            double totalPeople = 0;
            double weightedSumPrefk = 0;
            
            foreach (var item in budgetData.Values)
            {
                item.i_u_ratio = _insuredUninsuredRatio;
                item.prefk = (item.nr_mhi * (item.i_u_ratio - 1) + item.People) / item.People;
                
                totalPeople += item.People;
                weightedSumPrefk += item.prefk * item.People;
            }
            
            var prefk_w_avg = weightedSumPrefk / totalPeople;
            _logger.LogInformation($"Взвешенное среднее коэффициента предпочтения: {prefk_w_avg}");
        }
        
        private void CalculateBudgetMethod1(Dictionary<int, BudgetReplicationData> budgetData)
        {
            _logger.LogInformation("Расчет бюджетов по методу 1 (репликация расчетов Excel)");
            
            // Расчет взвешенных средних для географических коэффициентов
            double totalPopulation = budgetData.Values.Sum(d => d.Все_нас_2023);
            double weightedSumNs = budgetData.Values.Sum(d => d.geok_old_ns * d.Все_нас_2023);
            double weightedSumGsv = budgetData.Values.Sum(d => d.geok_old_gsv * d.Все_нас_2023);
            
            double geok_old_ns_w_avg = weightedSumNs / totalPopulation;
            double geok_old_gsv_w_avg = weightedSumGsv / totalPopulation;
            
            _logger.LogInformation($"Взвешенное среднее старого географического коэффициента для узких специалистов: {geok_old_ns_w_avg}");
            _logger.LogInformation($"Взвешенное среднее старого географического коэффициента для семейной медицины: {geok_old_gsv_w_avg}");
            
            // Расчет подушевого норматива
            double totalPeopleNS = budgetData.Values.Sum(d => d.people_NS);
            double totalPeople = budgetData.Values.Sum(d => d.People);
            
            // Расчет взвешенного среднего для коэффициента предпочтения
            double weightedSumPrefk = budgetData.Values.Sum(d => d.prefk * d.People);
            double prefk_w_avg = weightedSumPrefk / totalPeople;
            
            double capita1 = _source1 / (totalPeopleNS * geok_old_ns_w_avg);
            double capita2 = _source2 / (totalPeople * geok_old_gsv_w_avg * prefk_w_avg);
            
            _logger.LogInformation($"Базовый подушевой норматив для узких специалистов: {capita1}");
            _logger.LogInformation($"Базовый подушевой норматив для семейной медицины: {capita2}");
            
            // Расчет бюджетов для каждой организации
            double totalBudget1 = 0;
            double totalBudget2 = 0;
            
            foreach (var item in budgetData.Values)
            {
                item.budget_repl_1 = capita1 * item.people_NS * item.geok_old_ns;
                item.budget_repl_2 = capita2 * item.People * item.geok_old_gsv * item.prefk;
                item.budget_repl_tot = item.budget_repl_1 + item.budget_repl_2;
                
                totalBudget1 += item.budget_repl_1;
                totalBudget2 += item.budget_repl_2;
            }
            
            _logger.LogInformation($"Распределенный бюджет источника 1: {totalBudget1}");
            _logger.LogInformation($"Распределенный бюджет источника 2: {totalBudget2}");
            _logger.LogInformation($"Общий распределенный бюджет: {totalBudget1 + totalBudget2}");
            _logger.LogInformation($"Доступный бюджет: {_source1 + _source2}");
        }
        
        private void CalculateBudgetMethod2(Dictionary<int, BudgetReplicationData> budgetData)
        {
            _logger.LogInformation("Расчет бюджетов по методу 2 (скорректированный метод)");
            
            // Расчет подушевого норматива по скорректированному методу
            double weightedSumNS = budgetData.Values.Sum(d => d.people_NS * d.geok_old_ns);
            double weightedSumFM = budgetData.Values.Sum(d => d.People * d.geok_old_gsv * d.prefk);
            
            double pcrate1 = _source1 / weightedSumNS;
            double pcrate2 = _source2 / weightedSumFM;
            
            _logger.LogInformation($"Подушевой норматив для узких специалистов: {pcrate1}");
            _logger.LogInformation($"Подушевой норматив для семейной медицины: {pcrate2}");
            
            // Расчет бюджетов для каждой организации
            double totalBudget1 = 0;
            double totalBudget2 = 0;
            
            foreach (var item in budgetData.Values)
            {
                item.budget_repl_1 = pcrate1 * item.people_NS * item.geok_old_ns;
                item.budget_repl_2 = pcrate2 * item.People * item.geok_old_gsv * item.prefk;
                item.budget_repl_tot = item.budget_repl_1 + item.budget_repl_2;
                
                totalBudget1 += item.budget_repl_1;
                totalBudget2 += item.budget_repl_2;
            }
            
            _logger.LogInformation($"Распределенный бюджет источника 1: {totalBudget1}");
            _logger.LogInformation($"Распределенный бюджет источника 2: {totalBudget2}");
            _logger.LogInformation($"Общий распределенный бюджет: {totalBudget1 + totalBudget2}");
            _logger.LogInformation($"Доступный бюджет: {_source1 + _source2}");
        }
        
        private void CalculateDeviations(Dictionary<int, BudgetReplicationData> budgetData)
        {
            // Расчет отклонения бюджета и соотношения застрахованного населения
            foreach (var item in budgetData.Values)
            {
                item.budget_repl = -1 + item.budget_repl_tot / (item.ЦСМ_ГСВ_2023 * 1000);
                item.ins_repl = -1 + item.nr_mhi / item.застрах_нас_2023;
            }
        }
        
        private async Task SaveDataAsync<T>(T data, string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            _logger.LogInformation($"Сохранение данных в: {filePath}");
            
            string json = JsonSerializer.Serialize(data);
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInformation("Данные успешно сохранены");
        }
        
        private async Task CreateBudgetReplicationGraphsAsync(Dictionary<int, BudgetReplicationData> budgetData)
        {
            _logger.LogInformation("Создание графиков для воспроизведения бюджетов");
            
            // Сортировка данных по отклонению
            var sortedData = budgetData.Values
                .OrderBy(d => d.budget_repl)
                .ToArray();
            
            // Создание графика точности
            await CreateBudgetReplicationAccuracyGraphAsync(sortedData);
            
            // Создание гистограммы отклонений
            await CreateBudgetReplicationHistogramAsync(sortedData);
        }
        
        private async Task CreateBudgetReplicationAccuracyGraphAsync(BudgetReplicationData[] sortedData)
        {
            _logger.LogInformation("Создание графика точности воспроизведения бюджетов");
            
            var model = new PlotModel 
            { 
                Title = "Точность воспроизведения бюджетов",
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
                Title = "Значение коэффициента",
                TitleFontSize = 14,
                TitleFontWeight = FontWeights.Bold,
                AxisTitleDistance = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColors.LightGray,
                Minimum = -20,
                Maximum = 20,
                MajorStep = 5,
                MinorStep = 1,
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
                AxislineColor = OxyColors.Black,
                GapWidth = 0.1
            };

            model.Axes.Add(xAxis);
            model.Axes.Add(yAxis);
            
            // Группировка данных по регионам
            var groupedData = sortedData.GroupBy(d => d.Region);
            
            // Добавление данных
            int categoryIndex = 0;
            foreach (var group in groupedData)
            {
                var color = GetColorForRegion(group.Key);
                var series = new BarSeries
                {
                    Title = group.Key,
                    FillColor = color,
                    StrokeColor = OxyColors.Black,
                    StrokeThickness = 1,
                    BarWidth = 0.5,
                    BaseValue = 0
                };
                
                foreach (var item in group)
                {
                    series.Items.Add(new BarItem { Value = item.budget_repl * 100, CategoryIndex = categoryIndex });
                    yAxis.Labels.Add(item.NewName);
                    categoryIndex++;
                }
                
                model.Series.Add(series);
            }

            // Настройка отображения
            model.PlotMargins = new OxyThickness(120, 40, 40, 40); // Увеличиваем левое поле для названий
            yAxis.MinimumRange = sortedData.Length;
            yAxis.MaximumRange = sortedData.Length;
            yAxis.IsZoomEnabled = false;
            yAxis.IsPanEnabled = false;
            
            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, "budget_replication_accuracy.png");
            using (var stream = File.Create(outputPath))
            {
                var pngExporter = new OxyPlot.ImageSharp.PngExporter(1200, 1800);
                pngExporter.Export(model, stream);
            }
            
            _logger.LogInformation($"График точности сохранен в: {outputPath}");
        }
        
        private async Task CreateBudgetReplicationHistogramAsync(BudgetReplicationData[] sortedData)
        {
            _logger.LogInformation("Создание гистограммы отклонений бюджета");
            
            var model = new PlotModel
            {
                Title = "Histogram of the replication ratio (0 = perfect replication)\nГистограмма коэффициента репликации (0 = идеальная репликация)",
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
                Title = "Ratio",
                TitleFontSize = 12,
                TitleFontWeight = FontWeights.Bold,
                AxisTitleDistance = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColors.LightGray,
                StringFormat = "F2",
                TickStyle = TickStyle.Outside,
                AxislineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.Black
            };

            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Frequency",
                TitleFontSize = 12,
                TitleFontWeight = FontWeights.Bold,
                AxisTitleDistance = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColors.LightGray,
                Minimum = 0,
                TickStyle = TickStyle.Outside,
                AxislineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.Black
            };

            model.Axes.Add(xAxis);
            model.Axes.Add(yAxis);

            // Используем непосредственно budget_repl как в Python
            var values = sortedData.Select(d => d.budget_repl).ToArray();
            
            // Вычисляем границы и количество бинов как в Python
            var min = values.Min();
            var max = values.Max();
            const double binWidth = 0.01;
            var numBins = (int)((max - min) / binWidth);

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

            // Автоматическое определение пределов осей
            xAxis.Minimum = min;
            xAxis.Maximum = max;
            xAxis.MajorStep = 0.1;
            xAxis.MinorStep = 0.02;
            
            yAxis.Maximum = bins.Max() + 1;
            yAxis.MajorStep = 1;
            yAxis.MinorStep = 0.5;

            // Настройка отображения
            model.PlotMargins = new OxyThickness(60, 40, 20, 40);

            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, "budget_replication_histogram.png");
            using (var stream = File.Create(outputPath))
            {
                var pngExporter = new OxyPlot.ImageSharp.PngExporter(800, 600);
                pngExporter.Export(model, stream);
            }

            _logger.LogInformation($"Гистограмма отклонений сохранена в: {outputPath}");
        }
        
        private OxyColor GetColorForRegion(string region)
        {
            return _regionColors.TryGetValue(region, out var color) ? color : OxyColors.Gray;
        }
    }
}