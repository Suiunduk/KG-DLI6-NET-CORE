using System.Text.Json;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;
using KG_DLI6_NET_CORE.Models;

namespace KG_DLI6_NET_CORE.Services
{
    public class WorkloadGraphService
    {
        private readonly ILogger<WorkloadGraphService> _logger;
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

        public WorkloadGraphService(ILogger<WorkloadGraphService> logger)
        {
            _logger = logger;
            
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
            
            Directory.CreateDirectory(_workingPath);
            Directory.CreateDirectory(_outputPath);
        }
        
        public async Task CreateWorkloadGraphsAsync()
        {
            _logger.LogInformation("Начало создания графиков рабочей нагрузки");
            
            var workloadData = await LoadWorkloadDataAsync();
            var upMax = await LoadParameterAsync("upmax_value");
            var downMax = await LoadParameterAsync("downmax_value");
            
            _logger.LogInformation($"Загружены параметры: upMax = {upMax}, downMax = {downMax}");
            
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
            _logger.LogInformation($"Размер прочитанных данных: {json.Length} байт");
            
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogError("Файл пуст");
                throw new InvalidOperationException("Файл с данными пуст");
            }

            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<int, WorkloadData>>(json);
                _logger.LogInformation($"Данные успешно десериализованы. Количество записей: {data?.Count ?? 0}");
                
                if (data == null || data.Count == 0)
                {
                    _logger.LogError("После десериализации данные отсутствуют");
                    throw new InvalidOperationException("Не удалось получить данные после десериализации");
                }

                // Проверка данных
                foreach (var kvp in data)
                {
                    if (kvp.Value == null)
                    {
                        _logger.LogWarning($"Пустое значение для ключа {kvp.Key}");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(kvp.Value.NewName))
                    {
                        _logger.LogWarning($"Отсутствует название для организации с кодом {kvp.Key}");
                    }

                    if (kvp.Value.WorkloadCoefficient <= 0)
                    {
                        _logger.LogWarning($"Нулевой или отрицательный коэффициент для организации {kvp.Value.NewName} (код {kvp.Key}): {kvp.Value.WorkloadCoefficient}");
                    }
                }

                return data;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Ошибка при десериализации данных");
                throw new InvalidOperationException("Не удалось преобразовать данные из JSON", ex);
            }
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
            
            if (workloadData == null || !workloadData.Any())
            {
                _logger.LogError("Данные о рабочей нагрузке пусты или отсутствуют");
                throw new InvalidOperationException("Нет данных для построения графика");
            }

            _logger.LogInformation($"Количество записей для графика: {workloadData.Count}");
            
            var sortedData = workloadData.Values
                .OrderBy(w => w.Region)
                .ThenBy(w => w.NewName)
                .ToArray();

            _logger.LogInformation($"Отсортированные данные: {sortedData.Length} записей");
            _logger.LogInformation($"Пример первой записи: Region={sortedData[0].Region}, Name={sortedData[0].NewName}, Coefficient={sortedData[0].WorkloadCoefficient}");

            var model = new PlotModel
            {
                Title = "Коэффициенты рабочей нагрузки",
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
                Minimum = 0,
                Maximum = 2,
                MajorStep = 0.2,
                MinorStep = 0.1,
                TickStyle = TickStyle.Outside,
                AxislineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.Black
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
                Title = "Коэффициенты",
                FillColor = OxyColors.SteelBlue,
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                BarWidth = 0.8,
                BaseValue = 0
            };

            // Добавление данных
            for (int i = 0; i < sortedData.Length; i++)
            {
                var item = sortedData[i];
                if (item.WorkloadCoefficient <= 0)
                {
                    _logger.LogWarning($"Нулевой или отрицательный коэффициент для {item.NewName}: {item.WorkloadCoefficient}");
                }
                series.Items.Add(new BarItem { Value = item.WorkloadCoefficient, CategoryIndex = i });
                yAxis.Labels.Add(item.NewName);
            }

            model.Series.Add(series);

            // Настройка отображения
            model.PlotMargins = new OxyThickness(120, 40, 40, 40); // Увеличиваем левое поле для названий
            yAxis.MinimumRange = sortedData.Length;
            yAxis.MaximumRange = sortedData.Length;
            yAxis.GapWidth = 0.1;
            yAxis.IsZoomEnabled = false;
            yAxis.IsPanEnabled = false;

            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, "Fig3-WorkloadCoefficient.png");
            using (var stream = File.Create(outputPath))
            {
                var pngExporter = new OxyPlot.ImageSharp.PngExporter(1200, 1800);
                pngExporter.Export(model, stream);
            }

            _logger.LogInformation($"График коэффициентов рабочей нагрузки сохранен в: {outputPath}");
        }
        
        private async Task CreateWorkloadWithLimitsGraphAsync(Dictionary<int, WorkloadData> workloadData, double upMax, double downMax)
        {
            _logger.LogInformation("Создание графика рабочей нагрузки с ограничениями");
            
            if (workloadData == null || !workloadData.Any())
            {
                _logger.LogError("Данные о рабочей нагрузке пусты или отсутствуют");
                throw new InvalidOperationException("Нет данных для построения графика");
            }

            _logger.LogInformation($"Количество записей для графика: {workloadData.Count}");
            _logger.LogInformation($"Параметры ограничений: upMax={upMax}, downMax={downMax}");
            
            var sortedData = workloadData.Values
                .OrderBy(w => w.Region)
                .ThenBy(w => w.NewName)
                .ToArray();

            _logger.LogInformation($"Отсортированные данные: {sortedData.Length} записей");
            _logger.LogInformation($"Пример первой записи: Region={sortedData[0].Region}, Name={sortedData[0].NewName}, Coefficient={sortedData[0].AdjustedWorkloadCoefficient}");

            var model = new PlotModel
            {
                Title = "Скорректированные коэффициенты рабочей нагрузки",
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
                Minimum = 0,
                Maximum = 2,
                MajorStep = 0.2,
                MinorStep = 0.1,
                TickStyle = TickStyle.Outside,
                AxislineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.Black
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
                Title = "Коэффициенты",
                FillColor = OxyColors.SteelBlue,
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                BarWidth = 0.8,
                BaseValue = 0
            };

            // Добавление данных
            for (int i = 0; i < sortedData.Length; i++)
            {
                var item = sortedData[i];
                if (item.AdjustedWorkloadCoefficient <= 0)
                {
                    _logger.LogWarning($"Нулевой или отрицательный скорректированный коэффициент для {item.NewName}: {item.AdjustedWorkloadCoefficient}");
                }
                series.Items.Add(new BarItem { Value = item.AdjustedWorkloadCoefficient, CategoryIndex = i });
                yAxis.Labels.Add(item.NewName);
            }

            model.Series.Add(series);

            // Настройка отображения
            model.PlotMargins = new OxyThickness(120, 40, 40, 40); // Увеличиваем левое поле для названий
            yAxis.MinimumRange = sortedData.Length;
            yAxis.MaximumRange = sortedData.Length;
            yAxis.GapWidth = 0.1;
            yAxis.IsZoomEnabled = false;
            yAxis.IsPanEnabled = false;

            // Сохранение графика
            var outputPath = Path.Combine(_outputPath, "Fig4-AdjWorkloadCoefficient.png");
            using (var stream = File.Create(outputPath))
            {
                var pngExporter = new OxyPlot.ImageSharp.PngExporter(1200, 1800);
                pngExporter.Export(model, stream);
            }

            _logger.LogInformation($"График скорректированных коэффициентов рабочей нагрузки сохранен в: {outputPath}");
        }
        
        private OxyColor GetColorForRegion(string region)
        {
            return _regionColors.TryGetValue(region, out var color) ? color : OxyColors.Gray;
        }
    }
}