using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Globalization;
using KG_DLI6_NET_CORE.Models;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using CsvHelper;
using CsvHelper.Configuration;

namespace KG_DLI6_NET_CORE.Services
{
    public class HealthFacilityDataService
    {
        private readonly ILogger<HealthFacilityDataService> _logger;
        private readonly string _workingPath;
        private readonly string _originalPath;

        public HealthFacilityDataService(ILogger<HealthFacilityDataService> logger)
        {
            _logger = logger;
            _workingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "working");
            _originalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "original");

            if (!Directory.Exists(_workingPath))
                Directory.CreateDirectory(_workingPath);
            if (!Directory.Exists(_originalPath))
                Directory.CreateDirectory(_originalPath);
        }

        public async Task<List<HealthFacilityData>> ProcessHealthFacilityDataAsync()
        {
            _logger.LogInformation("Начало обработки данных медицинских учреждений");

            try
            {
                // Определение кодировки файла
                var encoding = await DetectFileEncodingAsync();
                
                // Загрузка данных
                var facilities = await LoadHealthFacilityDataAsync(encoding);
                
                // Сохранение результатов
                await SaveResultsAsync(facilities);
                
                _logger.LogInformation($"Завершена обработка данных медицинских учреждений. Всего записей: {facilities.Count}");
                return facilities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке данных медицинских учреждений");
                throw;
            }
        }

        private async Task<Encoding> DetectFileEncodingAsync()
        {
            var filePath = Path.Combine(_originalPath, "oz.csv");
            using (var stream = File.OpenRead(filePath))
            {
                var buffer = new byte[4096];
                await stream.ReadAsync(buffer, 0, buffer.Length);
                
                // Определение кодировки на основе содержимого файла
                if (buffer.All(b => b < 0x80))
                    return Encoding.ASCII;
                    
                if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    return Encoding.Unicode;
                    
                if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                    return Encoding.BigEndianUnicode;
                    
                if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    return Encoding.UTF8;
                    
                return Encoding.UTF8; // По умолчанию используем UTF-8
            }
        }

        private async Task<List<HealthFacilityData>> LoadHealthFacilityDataAsync(Encoding encoding)
        {
            var filePath = Path.Combine(_originalPath, "oz.csv");
            var facilities = new List<HealthFacilityData>();
            
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = "\t",
                Encoding = encoding,
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            };
            
            using (var reader = new StreamReader(filePath, encoding))
            using (var csv = new CsvReader(reader, config))
            {
                await foreach (var record in csv.GetRecordsAsync<HealthFacilityData>())
                {
                    facilities.Add(record);
                }
            }
            
            return facilities;
        }

        private async Task SaveResultsAsync(List<HealthFacilityData> facilities)
        {
            // Сохранение в Excel
            var excelPath = Path.Combine(_workingPath, "ozdata.xlsx");
            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                var worksheet = package.Workbook.Worksheets.Add("Данные");
                
                // Заголовки
                var properties = typeof(HealthFacilityData).GetProperties();
                for (int i = 0; i < properties.Length; i++)
                {
                    worksheet.Cells[1, i + 1].Value = properties[i].Name;
                }
                
                // Данные
                for (int i = 0; i < facilities.Count; i++)
                {
                    var facility = facilities[i];
                    for (int j = 0; j < properties.Length; j++)
                    {
                        worksheet.Cells[i + 2, j + 1].Value = properties[j].GetValue(facility)?.ToString();
                    }
                }
                
                await package.SaveAsync();
            }
            
            _logger.LogInformation($"Результаты сохранены в файл {excelPath}");
        }
    }
} 