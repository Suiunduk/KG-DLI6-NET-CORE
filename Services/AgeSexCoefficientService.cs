using System.Text.Json;
using ClosedXML.Excel;
using KG_DLI6_NET_CORE.Models;

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
            
            // Создаем директории, если они не существуют
            Directory.CreateDirectory(_workingPath);
            Directory.CreateDirectory(_outputPath);
        }
        
        public async Task CalculateAgeSexCoefficientsAsync()
        {
            _logger.LogInformation("Начало расчета возрастно-половых коэффициентов");
            
            // Загрузка данных
            var mpop = await LoadDataAsync<Dictionary<int, Dictionary<int, double>>>("mpop");
            var fpop = await LoadDataAsync<Dictionary<int, Dictionary<int, double>>>("fpop");
            var mvisits = await LoadDataAsync<Dictionary<int, Dictionary<int, double>>>("mvisits");
            var fvisits = await LoadDataAsync<Dictionary<int, Dictionary<int, double>>>("fvisits");
            
            // Расчет коэффициентов
            var (coeff, mcoeff, fcoeff, coeffsqu) = CalculateCoefficients(mpop, fpop, mvisits, fvisits);
            
            // Сохранение коэффициентов
            await SaveDataAsync(coeff, "coeff");
            await SaveDataAsync(mcoeff, "mcoeff");
            await SaveDataAsync(fcoeff, "fcoeff");
            await SaveDataAsync(coeffsqu, "coeffsqu");
            
            // Экспорт в Excel
            await ExportToExcelAsync(coeffsqu);
            
            // Создание графика здесь не реализовано - для этого можно использовать 
            // библиотеку для работы с графиками, например, LiveCharts2 или ScottPlot
            
            _logger.LogInformation("Расчет возрастно-половых коэффициентов завершен");
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
        
        private (Dictionary<int, double>, Dictionary<int, double>, Dictionary<int, double>, Dictionary<int, Dictionary<string, double>>) 
        CalculateCoefficients(
            Dictionary<int, Dictionary<int, double>> mpop, 
            Dictionary<int, Dictionary<int, double>> fpop, 
            Dictionary<int, Dictionary<int, double>> mvisits, 
            Dictionary<int, Dictionary<int, double>> fvisits)
        {
            // Общая сумма посещений и населения
            double Vall = SumAllValues(mvisits) + SumAllValues(fvisits);
            double Pall = SumAllValues(mpop) + SumAllValues(fpop);
            double vpc_all = Vall / Pall;
            
            _logger.LogInformation($"Количество посещений на душу населения во всем наборе данных: {vpc_all}");
            
            // Суммы посещений и населения по возрастам
            var Vmsum = SumByAge(mvisits);
            var Vfsum = SumByAge(fvisits);
            var Pmsum = SumByAge(mpop);
            var Pfsum = SumByAge(fpop);
            
            // Расчет коэффициентов
            var mcoeff = new Dictionary<int, double>();
            var fcoeff = new Dictionary<int, double>();
            
            for (int age = 0; age < 100; age++)
            {
                double mcoeffValue = (Pmsum[age] > 0) ? (Vmsum[age] / Pmsum[age]) / vpc_all : 0;
                double fcoeffValue = (Pfsum[age] > 0) ? (Vfsum[age] / Pfsum[age]) / vpc_all : 0;
                
                mcoeff[age] = mcoeffValue;
                fcoeff[age] = fcoeffValue;
            }
            
            // Объединение коэффициентов в один словарь
            var coeff = new Dictionary<int, double>();
            for (int age = 0; age < 100; age++)
            {
                coeff[age] = mcoeff[age];
                coeff[age + 100] = fcoeff[age];
            }
            
            // Создание матрицы с коэффициентами (мужчины и женщины в разных столбцах)
            var coeffsqu = new Dictionary<int, Dictionary<string, double>>();
            for (int age = 0; age < 100; age++)
            {
                coeffsqu[age] = new Dictionary<string, double>
                {
                    ["mcoeff"] = mcoeff[age],
                    ["fcoeff"] = fcoeff[age]
                };
            }
            
            return (coeff, mcoeff, fcoeff, coeffsqu);
        }
        
        private double SumAllValues(Dictionary<int, Dictionary<int, double>> data)
        {
            double sum = 0;
            foreach (var ageDict in data.Values)
            {
                foreach (var value in ageDict.Values)
                {
                    sum += value;
                }
            }
            return sum;
        }
        
        private Dictionary<int, double> SumByAge(Dictionary<int, Dictionary<int, double>> data)
        {
            var result = new Dictionary<int, double>();
            
            for (int age = 0; age < 100; age++)
            {
                if (data.ContainsKey(age))
                {
                    result[age] = data[age].Values.Sum();
                }
                else
                {
                    result[age] = 0;
                }
            }
            
            return result;
        }
        
        private async Task SaveDataAsync<T>(T data, string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            _logger.LogInformation($"Сохранение данных в: {filePath}");
            
            string json = JsonSerializer.Serialize(data);
            await File.WriteAllTextAsync(filePath, json);
        }
        
        private async Task ExportToExcelAsync(Dictionary<int, Dictionary<string, double>> coeffsqu)
        {
            var filePath = Path.Combine(_outputPath, "ascoeff.xlsx");
            _logger.LogInformation($"Экспорт в Excel: {filePath}");
            
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Age-Sex Coefficients");
                
                // Добавление заголовков
                worksheet.Cell(1, 1).Value = "Age";
                worksheet.Cell(1, 2).Value = "mcoeff";
                worksheet.Cell(1, 3).Value = "fcoeff";
                
                // Добавление данных
                int row = 2;
                foreach (var kvp in coeffsqu.OrderBy(k => k.Key))
                {
                    worksheet.Cell(row, 1).Value = kvp.Key;
                    worksheet.Cell(row, 2).Value = kvp.Value["mcoeff"];
                    worksheet.Cell(row, 3).Value = kvp.Value["fcoeff"];
                    row++;
                }
                
                // Автоподбор ширины столбцов
                worksheet.Columns().AdjustToContents();
                
                // Сохранение файла
                workbook.SaveAs(filePath);
            }
        }
    }
}