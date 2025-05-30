using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Models;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using System.Text.RegularExpressions;

namespace KG_DLI6_NET_CORE.Services
{
    public class CensusDataService
    {
        private readonly ILogger<CensusDataService> _logger;
        private readonly string _workingPath;
        private readonly string _originalPath;

        public CensusDataService(ILogger<CensusDataService> logger)
        {
            _logger = logger;
            _workingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "working");
            _originalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "original");

            if (!Directory.Exists(_workingPath))
                Directory.CreateDirectory(_workingPath);
            if (!Directory.Exists(_originalPath))
                Directory.CreateDirectory(_originalPath);
            
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<List<CensusData>> ProcessCensusDataAsync()
        {
            _logger.LogInformation("Начало обработки данных переписи населения");

            try
            {
                // Загрузка основных данных
                var censusData = await LoadMainCensusDataAsync();
                
                // Загрузка корректировок для городов
                var cityCorrections = await LoadCityCorrectionsAsync();
                
                // Применение корректировок
                var correctedData = ApplyCorrections(censusData, cityCorrections);
                
                // Сохранение результатов
                await SaveResultsAsync(correctedData);
                
                _logger.LogInformation($"Завершена обработка данных переписи населения. Всего записей: {correctedData.Count}");
                return correctedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке данных переписи населения");
                throw;
            }
        }

        private async Task<List<CensusData>> LoadMainCensusDataAsync()
        {
            var mainFile = Path.Combine(_originalPath, "9-С-01.01.2023 ОСНОВНОЙ.xlsx");
            var sheetNames = new[] { "Баткен", "Джалал", "Иссык", "нарын", "Ошская", "Талас", "Чуй", "КР,Ош+Бишкек" };
            var numRows = new[] { 307, 648, 333, 281, 741, 179, 574, 22 };
            
            var allData = new List<CensusData>();
            
            using (var package = new ExcelPackage(new FileInfo(mainFile)))
            {
                for (int i = 0; i < sheetNames.Length; i++)
                {
                    var worksheet = package.Workbook.Worksheets[sheetNames[i]];
                    var rows = numRows[i];
                    
                    for (int row = 5; row < 5 + rows; row++)
                    {
                        var soateNp = worksheet.Cells[row, 1].Text?.Trim();
                        var name = worksheet.Cells[row, 2].Text?.Trim();
                        var population = worksheet.Cells[row, 3].Text?.Trim();
                        
                        if (string.IsNullOrEmpty(soateNp) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(population))
                            continue;
                            
                        var censusData = new CensusData
                        {
                            SoateNp = soateNp.Replace(" ", ""),
                            Name = name,
                            Population = ParsePopulation(population),
                            Type = DetermineType(soateNp),
                            SoateOblast = soateNp.Substring(0, 5),
                            SoateRayon = soateNp.Substring(0, 8),
                            SoateAimak = DetermineSoateAimak(soateNp)
                        };
                        
                        allData.Add(censusData);
                    }
                }
            }
            
            return allData;
        }

        private async Task<Dictionary<string, double>> LoadCityCorrectionsAsync()
        {
            var correctionsFile = Path.Combine(_originalPath, "city_adjustments.xlsx");
            var corrections = new Dictionary<string, double>();
            
            using (var package = new ExcelPackage(new FileInfo(correctionsFile)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                var row = 2;
                
                while (!string.IsNullOrEmpty(worksheet.Cells[row, 1].Text))
                {
                    var total = worksheet.Cells[row, 1].Text.Trim();
                    var sum = 0.0;
                    
                    for (int col = 2; col <= 13; col++)
                    {
                        var value = worksheet.Cells[row, col].Text.Trim();
                        if (!string.IsNullOrEmpty(value))
                        {
                            sum += ParsePopulation(value);
                        }
                    }
                    
                    corrections[total] = sum;
                    row++;
                }
            }
            
            return corrections;
        }

        private List<CensusData> ApplyCorrections(List<CensusData> data, Dictionary<string, double> corrections)
        {
            foreach (var item in data)
            {
                item.CorrectedName = item.Name
                    .Replace("включая поселки, пгт.", "без сел/пгт.")
                    .Replace("включая села, пгт.", "без сел/пгт.")
                    .Replace("включая пгт.", "без пгт.")
                    .Replace("включая села", "без сел");
                    
                if (corrections.TryGetValue(item.SoateNp, out double correction))
                {
                    item.Correction = correction;
                    item.CorrectedPopulation = item.Population - (int)correction;
                }
                else
                {
                    item.Correction = 0;
                    item.CorrectedPopulation = item.Population;
                }
            }
            
            return data;
        }

        private async Task SaveResultsAsync(List<CensusData> data)
        {
            var outputFile = Path.Combine(_workingPath, "poplocations.xlsx");
            
            using (var package = new ExcelPackage(new FileInfo(outputFile)))
            {
                var worksheet = package.Workbook.Worksheets.Add("Данные");
                
                // Заголовки
                worksheet.Cells[1, 1].Value = "соате_нp";
                worksheet.Cells[1, 2].Value = "наименование";
                worksheet.Cells[1, 3].Value = "население";
                worksheet.Cells[1, 4].Value = "type";
                worksheet.Cells[1, 5].Value = "соате_область";
                worksheet.Cells[1, 6].Value = "соате_район";
                worksheet.Cells[1, 7].Value = "соате_аймак/айыл";
                worksheet.Cells[1, 8].Value = "область";
                worksheet.Cells[1, 9].Value = "район";
                worksheet.Cells[1, 10].Value = "аймак/айыл";
                worksheet.Cells[1, 11].Value = "наименование_исправ";
                worksheet.Cells[1, 12].Value = "correction";
                worksheet.Cells[1, 13].Value = "население_исправ";
                
                // Данные
                for (int i = 0; i < data.Count; i++)
                {
                    var row = i + 2;
                    var item = data[i];
                    
                    worksheet.Cells[row, 1].Value = item.SoateNp;
                    worksheet.Cells[row, 2].Value = item.Name;
                    worksheet.Cells[row, 3].Value = item.Population;
                    worksheet.Cells[row, 4].Value = item.Type;
                    worksheet.Cells[row, 5].Value = item.SoateOblast;
                    worksheet.Cells[row, 6].Value = item.SoateRayon;
                    worksheet.Cells[row, 7].Value = item.SoateAimak;
                    worksheet.Cells[row, 8].Value = item.Oblast;
                    worksheet.Cells[row, 9].Value = item.Rayon;
                    worksheet.Cells[row, 10].Value = item.Aimak;
                    worksheet.Cells[row, 11].Value = item.CorrectedName;
                    worksheet.Cells[row, 12].Value = item.Correction;
                    worksheet.Cells[row, 13].Value = item.CorrectedPopulation;
                }
                
                await package.SaveAsync();
            }
            
            _logger.LogInformation($"Результаты сохранены в файл {outputFile}");
        }

        private int ParsePopulation(string value)
        {
            value = value.Trim()
                .Replace("нет населения", "0")
                .Replace("нет", "0")
                .Replace("не существует", "0")
                .Replace("с.Жаны-Жер и с.Джаны-Джер есть одно и то же село", "0");
                
            if (string.IsNullOrEmpty(value))
                return 0;
                
            value = Regex.Replace(value, @"\.\d+", "");
            return int.TryParse(value, out int result) ? result : 0;
        }

        private string DetermineType(string soateNp)
        {
            if (soateNp.Substring(5).All(c => c == '0'))
                return "область";
                
            if (soateNp.Substring(8, 3) == "000" && soateNp.Substring(11, 3) != "000")
                return "город";
                
            if (soateNp.Substring(8, 6) == "000000")
                return "район";
                
            if (soateNp.Substring(11, 3) == "000" || soateNp.Substring(11, 3) == "800")
                return "аймак/айыл";
                
            return "нp";
        }

        private string DetermineSoateAimak(string soateNp)
        {
            var type = DetermineType(soateNp);
            return type == "город" ? "" : soateNp.Substring(0, 11);
        }
    }
} 