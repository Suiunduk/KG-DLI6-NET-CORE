using System.Text.Json;
using ScottPlot;
using KG_DLI6_NET_CORE.Models;

namespace KG_DLI6_NET_CORE.Services
{
    public class WorkloadGraphService
    {
        private readonly ILogger<WorkloadGraphService> _logger;
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

        public WorkloadGraphService(ILogger<WorkloadGraphService> logger)
        {
            _logger = logger;
            
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
            
            // Создаем директории, если они не существуют
            Directory.CreateDirectory(_workingPath);
            Directory.CreateDirectory(_outputPath);
        }
        
        public async Task CreateWorkloadGraphsAsync()
        {
            _logger.LogInformation("Начало создания графиков рабочей нагрузки");
            
            // Загрузка данных
            var workloadData = await LoadWorkloadDataAsync();
            var upMax = await LoadParameterAsync("upmax_value");
            var downMax = await LoadParameterAsync("downmax_value");
            
            _logger.LogInformation($"Загружены параметры: upMax = {upMax}, downMax = {downMax}");
            
            // Создание графиков
            await CreateWorkloadWithoutLimitsGraphAsync(workloadData);
            await CreateWorkloadWithLimitsGraphAsync(workloadData, upMax, downMax);
            
            _logger.LogInformation("Создание графиков рабочей нагрузки завершено");
        }
        
        private async Task<Dictionary<int, WorkloadData>> LoadWorkloadDataAsync()
        {
            var filePath = Path.Combine(_workingPath, "workload");
            _logger.LogInformation($"Загрузка данных из: {filePath}");
            
            if (!File.Exists(filePath))
            {
                _logger.LogError($"Файл не найден: {filePath}");
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }
            
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<Dictionary<int, WorkloadData>>(json);
        }
        
        private async Task<double> LoadParameterAsync(string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            _logger.LogInformation($"Загрузка параметра из: {filePath}");
            
            if (!File.Exists(filePath))
            {
                _logger.LogError($"Файл не найден: {filePath}");
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }
            
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<double>(json);
        }
        
        private async Task CreateWorkloadWithoutLimitsGraphAsync(Dictionary<int, WorkloadData> workloadData)
        {
            _logger.LogInformation("Создание графика рабочей нагрузки без ограничений");
            
            // Сортировка данных по региону и названию
            var sortedData = workloadData.Values
                .OrderBy(w => w.Region)
                .ThenBy(w => w.NewName)
                .ToArray();
            
            // Создание графика
            var plt = new ScottPlot.Plot(1200, 1800);
            
            // Получение уникальных регионов для легенды
            var uniqueRegions = sortedData.Select(w => w.Region).Distinct().ToArray();
            
            // Поскольку нельзя задать разные цвета для столбцов, создадим отдельные графики для каждого региона
            var groupedData = sortedData.GroupBy(w => w.Region);
            
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
                        double[] barValues = new double[] { regionItems[i].WorkloadCoefficient };
                        var bar = plt.AddBar(barValues, barPositions);
                        bar.Color = color;
                    }
                }
                
                // Добавляем элемент в легенду
                plt.Legend(true);
            }
            
            // Подготовка данных для горизонтальной столбчатой диаграммы
            double[] positions = Enumerable.Range(0, sortedData.Length).Select(i => (double)i).ToArray();
            
            // Настройка осей
            plt.XAxis.ManualTickPositions(positions, sortedData.Select(w => w.NewName).ToArray());
            plt.XAxis.TickLabelStyle(rotation: 45, fontSize: 8);
            plt.XAxis.Label("Медицинские организации");
            
            plt.YAxis.Label("Коэффициент нагрузки");
            plt.SetAxisLimits(yMin: 0.75, yMax: 1.25);
            
            // Добавление горизонтальной линии на значении 1
            plt.AddHorizontalLine(1, System.Drawing.Color.Black, 1, LineStyle.Solid);
            
            // Добавление заголовка
            plt.Title("Коэффициент загруженности по ОЗ (2022)");
            
            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, "Fig3-WorkloadCoefficient.png");
            plt.SaveFig(outputPath);
            
            _logger.LogInformation($"График сохранен в: {outputPath}");
        }
        
        private async Task CreateWorkloadWithLimitsGraphAsync(Dictionary<int, WorkloadData> workloadData, double upMax, double downMax)
        {
            _logger.LogInformation("Создание графика рабочей нагрузки с ограничениями");
            
            // Сортировка данных по региону и названию
            var sortedData = workloadData.Values
                .OrderBy(w => w.Region)
                .ThenBy(w => w.NewName)
                .ToArray();
            
            // Создание графика
            var plt = new ScottPlot.Plot(1200, 1800);
            
            // Получение уникальных регионов для легенды
            var uniqueRegions = sortedData.Select(w => w.Region).Distinct().ToArray();
            
            // Поскольку нельзя задать разные цвета для столбцов, создадим отдельные графики для каждого региона
            var groupedData = sortedData.GroupBy(w => w.Region);
            
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
                        double[] barValues = new double[] { regionItems[i].AdjustedWorkloadCoefficient };
                        var bar = plt.AddBar(barValues, barPositions);
                        bar.Color = color;
                    }
                }
                
                // Добавляем элемент в легенду
                plt.Legend(true);
            }
            
            // Подготовка данных для подписей оси X
            double[] positions = Enumerable.Range(0, sortedData.Length).Select(i => (double)i).ToArray();
            
            // Настройка осей
            plt.XAxis.ManualTickPositions(positions, sortedData.Select(w => w.NewName).ToArray());
            plt.XAxis.TickLabelStyle(rotation: 45, fontSize: 8);
            plt.XAxis.Label("Медицинские организации");
            
            plt.YAxis.Label("Коэффициент нагрузки (с ограничениями)");
            plt.SetAxisLimits(yMin: 0.75, yMax: 1.25);
            
            // Добавление горизонтальных линий
            plt.AddHorizontalLine(1, System.Drawing.Color.Black, 1, LineStyle.Solid);
            plt.AddHorizontalLine(upMax, System.Drawing.Color.Green, 1, LineStyle.Solid);
            plt.AddHorizontalLine(downMax, System.Drawing.Color.Red, 1, LineStyle.Solid);
            
            // Добавление заголовка
            plt.Title("Коэффициент загруженности по ОЗ, с ограничениями (2022)");
            
            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, "Fig4-AdjWorkloadCoefficient.png");
            plt.SaveFig(outputPath);
            
            _logger.LogInformation($"График сохранен в: {outputPath}");
        }
        
        private System.Drawing.Color GetColorForRegion(string region)
        {
            return _regionColors.ContainsKey(region) 
                ? _regionColors[region] 
                : System.Drawing.Color.Gray; // Цвет по умолчанию
        }
    }
}