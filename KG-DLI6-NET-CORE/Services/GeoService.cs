using System.Text.Json;
using KG_DLI6_NET_CORE.Models;
using OfficeOpenXml;

namespace KG_DLI6_NET_CORE.Services
{
    public class GeoService
    {
        private readonly ILogger<GeoService> _logger;
        private readonly string _originalPath;
        private readonly string _workingPath;
        
        public GeoService(ILogger<GeoService> logger)
        {
            _logger = logger;
            
            _originalPath = Path.Combine(Directory.GetCurrentDirectory(), "original");
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            
            // Создаем директории, если они не существуют
            Directory.CreateDirectory(_originalPath);
            Directory.CreateDirectory(_workingPath);
            
            // Для использования EPPlus в некоммерческих проектах
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }
        
        public async Task<ILookup<string, GeoData>> ProcessGeoDataAsync()
        {
            _logger.LogInformation("Начало обработки географических данных");
            
            // Чтение данных из Excel-файла
            var geoData = await ExtractGeoDataAsync();
            
            // Расчет взвешенного среднего
            var rayonCoefficients = CalculateRayonCoefficients(geoData);
            
            // Сохранение данных
            await SaveGeoDataAsync(geoData, "nurdindata");
            
            _logger.LogInformation("Обработка географических данных завершена");
            
            return geoData.ToLookup(d => d.np_soate.ToString(), d => d);
        }
        
        private async Task<List<GeoData>> ExtractGeoDataAsync()
        {
            var filePath = Path.Combine(_originalPath, "geodata.xlsx");
            _logger.LogInformation($"Извлечение данных из: {filePath}");
            
            if (!File.Exists(filePath))
            {
                _logger.LogError($"Файл не найден: {filePath}");
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }
            
            var geoDataList = new List<GeoData>();
            
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets["Расчет"];
                if (worksheet == null)
                {
                    _logger.LogError("Лист 'Расчет' не найден в файле Excel");
                    throw new InvalidOperationException("Лист 'Расчет' не найден в файле Excel");
                }
                
                // Пропускаем первую строку (заголовок) и начинаем со второй
                var rowCount = worksheet.Dimension.Rows;
                
                for (int row = 3; row <= rowCount; row++) // Начинаем со строки 3 (учитывая 1 строку заголовка и 1 строку пропуска)
                {
                    // Проверяем, что ячейка с кодом области не пуста
                    var oblastSoate = worksheet.Cells[row, 2].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(oblastSoate))
                        continue;
                    
                    var geoData = new GeoData
                    {
                        oblast_name = worksheet.Cells[row, 1].Value?.ToString() ?? string.Empty,
                        oblast_soate = worksheet.Cells[row, 2].Value?.ToString() ?? "0",
                        rayon_name = worksheet.Cells[row, 3].Value?.ToString() ?? string.Empty,
                        rayon_soate = worksheet.Cells[row, 4].Value?.ToString() ?? "0",
                        np_soate = worksheet.Cells[row, 5].Value?.ToString() ?? "0",
                        np_name = worksheet.Cells[row, 6].Value?.ToString() ?? string.Empty,
                        np_pop = Convert.ToDouble(worksheet.Cells[row, 7].Value),
                        rayon_km_oblast = worksheet.Cells[row, 8].Value != null ? Convert.ToDouble(worksheet.Cells[row, 8].Value) : 0,
                        np_km_rayon = worksheet.Cells[row, 9].Value != null ? Convert.ToDouble(worksheet.Cells[row, 9].Value) : 0,
                        np_geocoeff = worksheet.Cells[row, 10].Value != null ? Convert.ToDouble(worksheet.Cells[row, 10].Value) : 0
                    };
                    
                    // Особый случай для г.Бишкек
                    if (geoData.rayon_name == "г.Бишкек")
                    {
                        geoData.rayon_soate = geoData.oblast_soate;
                    }
                    
                    geoDataList.Add(geoData);
                }
            }
            
            _logger.LogInformation($"Извлечено {geoDataList.Count} записей географических данных");
            
            return geoDataList;
        }
        
        private Dictionary<string, double> CalculateRayonCoefficients(List<GeoData> geoData)
        {
            _logger.LogInformation("Расчет взвешенных средних географических коэффициентов по районам");
            
            var rayonCoefficients = new Dictionary<string, double>();
            
            // Группировка данных по коду района
            var groupedByRayon = geoData
                .GroupBy(d => d.rayon_soate)
                .Select(group => new
                {
                    rayon_soate = group.Key,
                    rayon_geocoeff_w = group.Sum(d => d.np_geocoeff * d.np_pop) / group.Sum(d => d.np_pop)
                })
                .ToList();
            
            foreach (var rayon in groupedByRayon)
            {
                rayonCoefficients.Add(rayon.rayon_soate, rayon.rayon_geocoeff_w);
            }
            
            _logger.LogInformation($"Рассчитано {rayonCoefficients.Count} взвешенных коэффициентов для районов");
            
            return rayonCoefficients;
        }
        
        private async Task SaveGeoDataAsync<T>(T data, string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            _logger.LogInformation($"Сохранение данных в: {filePath}");
            
            string json = JsonSerializer.Serialize(data);
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInformation("Данные успешно сохранены");
        }
    }
}