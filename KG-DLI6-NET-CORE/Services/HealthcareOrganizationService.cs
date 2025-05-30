using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Models;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;

namespace KG_DLI6_NET_CORE.Services
{
    public class HealthcareOrganizationService
    {
        private readonly ILogger<HealthcareOrganizationService> _logger;
        private readonly string _workingPath;
        private readonly string _outputPath;

        public HealthcareOrganizationService(ILogger<HealthcareOrganizationService> logger)
        {
            _logger = logger;
            _workingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "working");
            _outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

            if (!Directory.Exists(_workingPath))
                Directory.CreateDirectory(_workingPath);
            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);
            
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<List<HealthcareOrganization>> ProcessHealthcareOrganizationsAsync()
        {
            _logger.LogInformation("Начало обработки данных о медицинских организациях");

            var organizations = new List<HealthcareOrganization>();
            var fileNames = new[]
            {
                "original/Ошская область 19.06.2024.xlsx",
                "original/Геолокация по Нарынской области 21.06.24.xlsx",
                "original/Геолакация Жалал-Абадская область 16.07.2024.xlsx"
            };

            var sheetNames = new[] { "Ошская", "Нарын", "Джалал" };
            var numRows = new[] { 741, 224, 648 };

            for (int i = 0; i < fileNames.Length; i++)
            {
                _logger.LogInformation($"Обработка файла {fileNames[i]}, лист {sheetNames[i]}");

                using var package = new ExcelPackage(new FileInfo(fileNames[i]));
                var worksheet = package.Workbook.Worksheets[sheetNames[i]];
                var startRow = 5;
                var endRow = startRow + numRows[i] - 1;

                for (int row = startRow; row <= endRow; row++)
                {
                    var org = new HealthcareOrganization
                    {
                        Oblast = sheetNames[i],
                        NpSoate = worksheet.Cells[row, 1].Text,
                        Np = worksheet.Cells[row, 2].Text,
                        NpPopulation = ParsePopulation(worksheet.Cells[row, 3].Text),
                        NpCoordinates = worksheet.Cells[row, 4].Text,
                        CsmCode = worksheet.Cells[row, 5].Text,
                        Csm = worksheet.Cells[row, 6].Text,
                        CsmAddress = worksheet.Cells[row, 7].Text,
                        CsmCoordinates = worksheet.Cells[row, 8].Text,
                        CsmLocation = worksheet.Cells[row, 9].Text,
                        GsvCode = worksheet.Cells[row, 10].Text,
                        Gsv = worksheet.Cells[row, 11].Text,
                        GsvAddress = worksheet.Cells[row, 12].Text,
                        GsvCoordinates = worksheet.Cells[row, 13].Text,
                        GsvLocation = worksheet.Cells[row, 14].Text,
                        FapCode = worksheet.Cells[row, 15].Text,
                        Fap = worksheet.Cells[row, 16].Text,
                        FapAddress = worksheet.Cells[row, 17].Text,
                        FapCoordinates = worksheet.Cells[row, 18].Text,
                        FapLocation = worksheet.Cells[row, 19].Text,
                        RowNumber = row
                    };

                    ProcessOrganization(org);
                    organizations.Add(org);
                }
            }

            await SaveOrganizationsAsync(organizations);
            _logger.LogInformation($"Завершена обработка данных о медицинских организациях. Обработано {organizations.Count} записей");

            return organizations;
        }

        private void ProcessOrganization(HealthcareOrganization org)
        {
            // Очистка данных
            org.NpSoate = CleanSoate(org.NpSoate);
            org.Np = CleanName(org.Np);
            org.CsmAddress = CleanAddress(org.CsmAddress);
            org.GsvAddress = CleanAddress(org.GsvAddress);
            org.FapAddress = CleanAddress(org.FapAddress);

            // Обработка множественных ФАП
            if (!string.IsNullOrEmpty(org.Fap))
            {
                org.FapCodes = org.FapCode.Split(';').Select(x => x.Trim()).ToList();
                org.FapNames = org.Fap.Split(';').Select(x => x.Trim()).ToList();
                org.FapCoordinatesList = org.FapCoordinates.Split(';').Select(x => x.Trim()).ToList();
                org.NFap = org.FapNames.Count;
            }

            // Обработка множественных ГСВ
            if (!string.IsNullOrEmpty(org.Gsv))
            {
                org.GsvCodes = org.GsvCode.Split(';').Select(x => x.Trim()).ToList();
                org.GsvNames = org.Gsv.Split(';').Select(x => x.Trim()).ToList();
                org.GsvCoordinatesList = org.GsvCoordinates.Split(';').Select(x => x.Trim()).ToList();
                org.NGsv = org.GsvNames.Count;
            }

            // Исправление ошибок в данных
            FixDataErrors(org);
        }

        private string CleanSoate(string soate)
        {
            if (string.IsNullOrEmpty(soate)) return soate;
            soate = soate.Replace("41706211800000", "41706211000000");
            return soate.Replace(" ", "");
        }

        private string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Trim().Replace("  ", " ");
        }

        private string CleanAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return address;
            return address.Replace("     ", " ")
                         .Replace("   ", " ")
                         .Replace("  ", " ")
                         .Trim();
        }

        private int ParsePopulation(string population)
        {
            if (string.IsNullOrEmpty(population)) return 0;
            population = population.Replace("не существует", "0")
                                 .Replace("нет населения", "0")
                                 .Replace("nan", "0")
                                 .Replace(".0", "");
            return int.TryParse(population, out int result) ? result : 0;
        }

        private void FixDataErrors(HealthcareOrganization org)
        {
            // Исправление ошибок в координатах
            if (org.NpSoate == "41703225600010")
                org.GsvCoordinates = "41°51'45.7 N 72°56'48.3 E";
            if (org.NpSoate == "41703225820050")
                org.GsvCode = "";

            if (org.NpSoate == "41706255832030")
                org.NpCoordinates = "40°43'52.2 N 73°12'32.4 E";
            if (org.NpSoate == "41706226812030")
            {
                org.NpCoordinates = "40.381417, 72.978076";
                org.GsvCoordinates = "40.381417, 72.978076";
            }
            if (org.NpSoate == "41706211835040")
                org.NpCoordinates = "40.518899, 72.451504";

            // Исправление ошибок в кодах
            if (string.IsNullOrEmpty(org.GsvCode) && org.Gsv == "9 поликлиника город Ош")
                org.GsvCode = "999999";

            // Удаление ФАП, которые на самом деле являются ГСВ
            if (!string.IsNullOrEmpty(org.Fap) && org.Fap.Contains("ГСВ"))
            {
                org.FapCode = "";
                org.Fap = "";
            }
        }

        private async Task SaveOrganizationsAsync(List<HealthcareOrganization> organizations)
        {
            var filePath = Path.Combine(_workingPath, "healthcare_organizations.json");
            var json = System.Text.Json.JsonSerializer.Serialize(organizations, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation($"Данные сохранены в файл {filePath}");
        }
    }
}

