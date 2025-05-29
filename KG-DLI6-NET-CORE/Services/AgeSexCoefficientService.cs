using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Models;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace KG_DLI6_NET_CORE.Services
{
    public class AgeSexCoefficientService
    {
        private readonly ILogger<AgeSexCoefficientService> _logger;
        private readonly string _workingPath;
        private readonly string _outputPath;

        public AgeSexCoefficientService(ILogger<AgeSexCoefficientService> logger)
        {
            _logger = logger;
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");

            if (!Directory.Exists(_workingPath))
                Directory.CreateDirectory(_workingPath);
            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);
        }

        public async Task<List<AgeSexCoefficient>> CalculateAgeSexCoefficientsAsync()
        {
            _logger.LogInformation("Начало расчета возрастно-половых коэффициентов");

            try
            {
                // Загрузка данных
                var malePopulation = await LoadDataAsync<Dictionary<int, Dictionary<int, double>>>("mpop");
                var femalePopulation = await LoadDataAsync<Dictionary<int, Dictionary<int, double>>>("fpop");
                var maleVisits = await LoadDataAsync<Dictionary<int, Dictionary<int, double>>>("mvisits");
                var femaleVisits = await LoadDataAsync<Dictionary<int, Dictionary<int, double>>>("fvisits");

                if (malePopulation == null || femalePopulation == null || 
                    maleVisits == null || femaleVisits == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить данные о населении или посещениях");
                }

                // Расчет общего количества посещений и населения
                var totalVisits = SumAllValues(maleVisits) + SumAllValues(femaleVisits);
                var totalPopulation = SumAllValues(malePopulation) + SumAllValues(femalePopulation);
                
                if (totalPopulation == 0)
                {
                    throw new InvalidOperationException("Общая численность населения равна нулю");
                }

                var visitsPerCapita = totalVisits / totalPopulation;
                _logger.LogInformation($"Среднее количество посещений на душу населения: {visitsPerCapita:F2}");

                // Расчет сумм по возрастам
                var maleVisitSums = CalculateAgeSums(maleVisits);
                var femaleVisitSums = CalculateAgeSums(femaleVisits);
                var malePopulationSums = CalculateAgeSums(malePopulation);
                var femalePopulationSums = CalculateAgeSums(femalePopulation);

                // Расчет коэффициентов
                var coefficients = new List<AgeSexCoefficient>();
                var maleCoefficients = new Dictionary<int, double>();
                var femaleCoefficients = new Dictionary<int, double>();

                // Для мужчин
                for (int age = 0; age < 100; age++)
                {
                    if (malePopulationSums.ContainsKey(age) && malePopulationSums[age] > 0)
                    {
                        var maleCoeff = (maleVisitSums[age] / malePopulationSums[age]) / visitsPerCapita;
                        coefficients.Add(new AgeSexCoefficient
                        {
                            Age = age.ToString(),
                            Sex = "М",
                            Coefficient = maleCoeff
                        });
                        maleCoefficients[age] = maleCoeff;
                    }
                }

                // Для женщин
                for (int age = 0; age < 100; age++)
                {
                    if (femalePopulationSums.ContainsKey(age) && femalePopulationSums[age] > 0)
                    {
                        var femaleCoeff = (femaleVisitSums[age] / femalePopulationSums[age]) / visitsPerCapita;
                        coefficients.Add(new AgeSexCoefficient
                        {
                            Age = age.ToString(),
                            Sex = "Ж",
                            Coefficient = femaleCoeff
                        });
                        femaleCoefficients[age] = femaleCoeff;
                    }
                }

                // Сохранение результатов
                await SaveResultsAsync(coefficients, maleCoefficients, femaleCoefficients);
                
                // Генерация графика
                await GenerateGraphAsync(coefficients);

                _logger.LogInformation($"Завершен расчет возрастно-половых коэффициентов. Всего записей: {coefficients.Count}");
                return coefficients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при расчете возрастно-половых коэффициентов");
                throw;
            }
        }

        private Dictionary<int, double> CalculateAgeSums(Dictionary<int, Dictionary<int, double>> data)
        {
            var sums = new Dictionary<int, double>();
            foreach (var age in data.Keys)
            {
                sums[age] = data[age].Values.Sum();
            }
            return sums;
        }

        private async Task<T> LoadDataAsync<T>(string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл {fileName} не найден");
            }

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }

        private async Task SaveResultsAsync(
            List<AgeSexCoefficient> coefficients,
            Dictionary<int, double> maleCoefficients,
            Dictionary<int, double> femaleCoefficients)
        {
            // Сохранение в JSON
            var json = JsonSerializer.Serialize(coefficients);
            await File.WriteAllTextAsync(Path.Combine(_workingPath, "age_sex_coefficients.json"), json);

            // Сохранение коэффициентов для мужского населения
            var maleCoefficientsJson = JsonSerializer.Serialize(maleCoefficients);
            await File.WriteAllTextAsync(Path.Combine(_workingPath, "mcoeff"), maleCoefficientsJson);

            // Сохранение коэффициентов для женского населения
            var femaleCoefficientsJson = JsonSerializer.Serialize(femaleCoefficients);
            await File.WriteAllTextAsync(Path.Combine(_workingPath, "fcoeff"), femaleCoefficientsJson);

            // Сохранение объединенных коэффициентов
            var combinedCoefficients = new Dictionary<int, Dictionary<string, double>>();
            for (int age = 0; age < 100; age++)
            {
                combinedCoefficients[age] = new Dictionary<string, double>();
                if (maleCoefficients.ContainsKey(age))
                    combinedCoefficients[age]["mcoeff"] = maleCoefficients[age];
                if (femaleCoefficients.ContainsKey(age))
                    combinedCoefficients[age]["fcoeff"] = femaleCoefficients[age];
            }
            var combinedJson = JsonSerializer.Serialize(combinedCoefficients);
            await File.WriteAllTextAsync(Path.Combine(_workingPath, "coeffsqu"), combinedJson);

            // Сохранение в Excel
            var excelPath = Path.Combine(_outputPath, "ascoeff.xlsx");
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                var worksheet = package.Workbook.Worksheets.Add("Коэффициенты");
                
                // Заголовки
                worksheet.Cells[1, 1].Value = "Возраст";
                worksheet.Cells[1, 2].Value = "Мужчины";
                worksheet.Cells[1, 3].Value = "Женщины";
                
                // Данные
                for (int age = 0; age < 100; age++)
                {
                    worksheet.Cells[age + 2, 1].Value = age;
                    if (maleCoefficients.ContainsKey(age))
                        worksheet.Cells[age + 2, 2].Value = maleCoefficients[age];
                    if (femaleCoefficients.ContainsKey(age))
                        worksheet.Cells[age + 2, 3].Value = femaleCoefficients[age];
                }
                
                await package.SaveAsync();
            }
            
            _logger.LogInformation($"Результаты сохранены в файлы age_sex_coefficients.json, mcoeff, fcoeff, coeffsqu и ascoeff.xlsx");
        }

        private async Task GenerateGraphAsync(List<AgeSexCoefficient> coefficients)
        {
            _logger.LogInformation("Создание графика половозрастных коэффициентов...");

            // Создаем два отдельных графика
            var plotModelMale = new PlotModel
            {
                Title = "mcoeff",
                TitleFontSize = 14,
                TitleFontWeight = FontWeights.Bold,
                PlotAreaBorderThickness = new OxyThickness(1),
                PlotAreaBorderColor = OxyColors.Black,
                Background = OxyColors.White,
                TextColor = OxyColors.Black,
                PlotMargins = new OxyThickness(60, 40, 20, 40)
            };

            var plotModelFemale = new PlotModel
            {
                Title = "fcoeff",
                TitleFontSize = 14,
                TitleFontWeight = FontWeights.Bold,
                PlotAreaBorderThickness = new OxyThickness(1),
                PlotAreaBorderColor = OxyColors.Black,
                Background = OxyColors.White,
                TextColor = OxyColors.Black,
                PlotMargins = new OxyThickness(60, 40, 20, 40)
            };

            // Настройка осей для мужского графика
            var maleYAxis = new LinearAxis 
            { 
                Position = AxisPosition.Left,
                Minimum = 0,
                Maximum = 8,
                MajorStep = 2,
                MinorStep = 1,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(211, 211, 211),
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColor.FromRgb(211, 211, 211),
                AxislineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.Black
            };

            var maleXAxis = new LinearAxis 
            { 
                Position = AxisPosition.Bottom,
                Minimum = 0,
                Maximum = 100,
                MajorStep = 20,
                MinorStep = 5,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(211, 211, 211),
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColor.FromRgb(211, 211, 211),
                AxislineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.Black
            };

            // Настройка осей для женского графика
            var femaleYAxis = new LinearAxis 
            { 
                Position = AxisPosition.Left,
                Minimum = 0,
                Maximum = 8,
                MajorStep = 2,
                MinorStep = 1,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(211, 211, 211),
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColor.FromRgb(211, 211, 211),
                AxislineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.Black
            };

            var femaleXAxis = new LinearAxis 
            { 
                Position = AxisPosition.Bottom,
                Title = "Age / Возраст",
                Minimum = 0,
                Maximum = 100,
                MajorStep = 20,
                MinorStep = 5,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.FromRgb(211, 211, 211),
                MinorGridlineStyle = LineStyle.Dot,
                MinorGridlineColor = OxyColor.FromRgb(211, 211, 211),
                AxislineStyle = LineStyle.Solid,
                AxislineColor = OxyColors.Black
            };

            plotModelMale.Axes.Add(maleYAxis);
            plotModelMale.Axes.Add(maleXAxis);
            plotModelFemale.Axes.Add(femaleYAxis);
            plotModelFemale.Axes.Add(femaleXAxis);

            // Создаем серии для мужчин и женщин
            var maleSeries = new LineSeries 
            {
                Color = OxyColor.FromRgb(31, 119, 180), // Синий цвет как в matplotlib
                StrokeThickness = 1,
                MarkerSize = 0
            };

            var femaleSeries = new LineSeries 
            {
                Color = OxyColor.FromRgb(255, 127, 14), // Оранжевый цвет как в matplotlib
                StrokeThickness = 1,
                MarkerSize = 0
            };

            // Добавляем данные
            var maleCoeffs = coefficients.Where(c => c.Sex == "М")
                .OrderBy(c => int.Parse(c.Age))
                .ToList();

            var femaleCoeffs = coefficients.Where(c => c.Sex == "Ж")
                .OrderBy(c => int.Parse(c.Age))
                .ToList();

            foreach (var coeff in maleCoeffs)
            {
                maleSeries.Points.Add(new DataPoint(double.Parse(coeff.Age), coeff.Coefficient));
            }

            foreach (var coeff in femaleCoeffs)
            {
                femaleSeries.Points.Add(new DataPoint(double.Parse(coeff.Age), coeff.Coefficient));
            }

            // Заполняем область под графиком
            var maleAreaSeries = new AreaSeries 
            {
                Color = OxyColor.FromRgb(31, 119, 180),
                Fill = OxyColor.FromRgb(31, 119, 180)
            };
            foreach (var point in maleSeries.Points)
            {
                maleAreaSeries.Points.Add(point);
            }

            var femaleAreaSeries = new AreaSeries 
            {
                Color = OxyColor.FromRgb(255, 127, 14),
                Fill = OxyColor.FromRgb(255, 127, 14)
            };
            foreach (var point in femaleSeries.Points)
            {
                femaleAreaSeries.Points.Add(point);
            }

            plotModelMale.Series.Add(maleAreaSeries);
            plotModelFemale.Series.Add(femaleAreaSeries);

            // Создаем общий заголовок
            var titleModel = new PlotModel
            {
                Title = "Age and sex coefficients / половозрастные коэффициенты (2022)",
                TitleFontSize = 20,
                TitleFontWeight = FontWeights.Bold,
                Background = OxyColors.White
            };

            // Сохранение графиков
            var outputPath = Path.Combine(_outputPath, "Fig2-AgeSexCoefficients.png");
            
            // Экспортируем каждый график отдельно в память
            byte[] titleImageBytes;
            byte[] maleImageBytes;
            byte[] femaleImageBytes;

            var titleExporter = new OxyPlot.ImageSharp.PngExporter(1100, 50);
            var graphExporter = new OxyPlot.ImageSharp.PngExporter(1100, 225);

            using (var titleStream = new MemoryStream())
            {
                titleExporter.Export(titleModel, titleStream);
                titleImageBytes = titleStream.ToArray();
            }

            using (var maleStream = new MemoryStream())
            {
                graphExporter.Export(plotModelMale, maleStream);
                maleImageBytes = maleStream.ToArray();
            }

            using (var femaleStream = new MemoryStream())
            {
                graphExporter.Export(plotModelFemale, femaleStream);
                femaleImageBytes = femaleStream.ToArray();
            }

            // Создаем и сохраняем финальное изображение
            using (var finalImage = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1100, 500))
            using (var titleImage = SixLabors.ImageSharp.Image.Load(titleImageBytes))
            using (var maleImage = SixLabors.ImageSharp.Image.Load(maleImageBytes))
            using (var femaleImage = SixLabors.ImageSharp.Image.Load(femaleImageBytes))
            {
                finalImage.Mutate(ctx => ctx
                    .DrawImage(titleImage, new SixLabors.ImageSharp.Point(0, 0), 1f)
                    .DrawImage(maleImage, new SixLabors.ImageSharp.Point(0, 50), 1f)
                    .DrawImage(femaleImage, new SixLabors.ImageSharp.Point(0, 275), 1f));

                // Сохраняем с использованием нового потока
                using (var outputStream = File.Create(outputPath))
                {
                    finalImage.Save(outputStream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                }
            }
            
            _logger.LogInformation("График сохранен в {OutputPath}", outputPath);
        }

        private double SumAllValues(Dictionary<int, Dictionary<int, double>> data)
        {
            double sum = 0;
            foreach (var ageDict in data.Values)
            {
                sum += ageDict.Values.Sum();
            }
            return sum;
        }
    }
} 