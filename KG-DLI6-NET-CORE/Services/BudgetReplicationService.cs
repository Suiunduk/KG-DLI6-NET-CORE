using System.Text.Json;
using KG_DLI6_NET_CORE.Models;
using ScottPlot;

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
        
        // Словарь цветов для областей, аналогичный Python-коду
        private readonly Dictionary<string, System.Drawing.Color> _regionColors = new()
        {
            { "Баткенская обл.", System.Drawing.Color.Red },
            { "Бишкек г.", System.Drawing.Color.Blue },
            { "Чуйская обл.", System.Drawing.Color.Green },
            { "Иссык-Кульская обл.", System.Drawing.Color.Black },
            { "Джалал-Абадская обл.", System.Drawing.Color.Magenta },
            { "Нарынская обл.", System.Drawing.Color.Green },
            { "Ошская обл.", System.Drawing.Color.Yellow },
            { "Таласская обл.", System.Drawing.Color.Cyan },
            { "Ош г.", System.Drawing.Color.Black }
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
            _logger.LogInformation("Создание графиков точности репликации бюджета");
            
            // Сортировка данных
            var sortedData = budgetData.Values
                .OrderBy(x => x.Region)
                .ThenBy(x => x.NewName)
                .ToArray();
            
            // 1. Создание графика точности репликации бюджета
            await CreateBudgetReplicationAccuracyGraphAsync(sortedData);
            
            // 2. Создание гистограммы коэффициента репликации
            await CreateBudgetReplicationHistogramAsync(sortedData);
            
            _logger.LogInformation("Графики точности репликации бюджета созданы");
        }
        
        private async Task CreateBudgetReplicationAccuracyGraphAsync(BudgetReplicationData[] sortedData)
        {
            var plt = new ScottPlot.Plot(1200, 1800);
            
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
                        double[] barValues = new double[] { regionItems[i].budget_repl };
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
            
            plt.YAxis.Label("% бюджета 2023 г.");
            plt.SetAxisLimits(yMin: -0.2, yMax: 0.2);
            
            // Добавление горизонтальной линии на значении 0
            plt.AddHorizontalLine(0, System.Drawing.Color.Black, 1, LineStyle.Solid);
            
            // Добавление заголовка
            plt.Title("Точность репликации бюджета (2023 г.)");
            
            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, "Fig5-budget replication quality.png");
            plt.SaveFig(outputPath);
            
            _logger.LogInformation($"График точности репликации бюджета сохранен в: {outputPath}");
        }
        
        private async Task CreateBudgetReplicationHistogramAsync(BudgetReplicationData[] sortedData)
        {
            var plt = new ScottPlot.Plot(1000, 800);
            
            // Получение значений отклонений
            double[] values = sortedData.Select(d => d.budget_repl).ToArray();
            
            // Расчет параметров гистограммы
            double min = values.Min();
            double max = values.Max();
            double binWidth = 0.01;
            int binCount = (int)((max - min) / binWidth);
            
            // Создание гистограммы с использованием методов ScottPlot 4.x
            double[] counts = new double[binCount];
            double[] binEdges = new double[binCount + 1];
            
            // Формируем границы бинов
            for (int i = 0; i <= binCount; i++)
            {
                binEdges[i] = min + i * binWidth;
            }
            
            // Заполняем бины
            foreach (var value in values)
            {
                int binIndex = (int)((value - min) / binWidth);
                if (binIndex >= 0 && binIndex < binCount)
                {
                    counts[binIndex]++;
                }
            }
            
            // Центры бинов для отображения на графике
            double[] binCenters = new double[binCount];
            for (int i = 0; i < binCount; i++)
            {
                binCenters[i] = min + (i + 0.5) * binWidth;
            }
            
            // Отображаем как бары
            var bar = plt.AddBar(counts, binCenters);
            bar.BarWidth = binWidth * 0.8;
            bar.FillColor = System.Drawing.Color.Blue;
            bar.BorderColor = System.Drawing.Color.Black;
            
            // Настройка осей
            plt.XAxis.Label("Коэффициент репликации");
            plt.YAxis.Label("Частота");
            
            // Добавление заголовка
            plt.Title("Гистограмма коэффициента репликации (0 = идеальная репликация)");
            
            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, "Fig6-budget replication histogram.png");
            plt.SaveFig(outputPath);
            
            _logger.LogInformation($"Гистограмма коэффициента репликации сохранена в: {outputPath}");
        }
        
        private System.Drawing.Color GetColorForRegion(string region)
        {
            return _regionColors.ContainsKey(region) 
                ? _regionColors[region] 
                : System.Drawing.Color.Gray; // Цвет по умолчанию
        }
    }
}