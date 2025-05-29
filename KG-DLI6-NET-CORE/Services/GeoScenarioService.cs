using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KG_DLI6_NET_CORE.Services
{
    public class GeoScenarioService
    {
        private readonly ILogger<GeoScenarioService> _logger;
        private readonly string _workingPath;
        private readonly string _outputPath;

        public GeoScenarioService(ILogger<GeoScenarioService> logger)
        {
            _logger = logger;
            _workingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "working");
            _outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

            if (!Directory.Exists(_workingPath))
                Directory.CreateDirectory(_workingPath);
            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);
        }

        public async Task<List<HealthcareOrganization>> CreateGeoScenariosAsync()
        {
            _logger.LogInformation("Начало создания географических сценариев");

            try
            {
                // Загрузка данных с расстояниями
                var organizations = await LoadOrganizationsWithDistancesAsync();
                
                // Сортировка организаций
                organizations = organizations.OrderBy(o => o.Oblast)
                                         .ThenBy(o => o.RowNumber)
                                         .ToList();
                
                // Создание сценариев
                var scenarios = await CreateScenariosAsync(organizations);
                
                // Сохранение результатов
                await SaveScenariosAsync(scenarios);
                
                _logger.LogInformation($"Завершено создание географических сценариев для {organizations.Count} медицинских организаций");
                return organizations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании географических сценариев");
                throw;
            }
        }

        private async Task<List<HealthcareOrganization>> LoadOrganizationsWithDistancesAsync()
        {
            var filePath = Path.Combine(_workingPath, "organizations_with_distances.json");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Файл с данными о расстояниях не найден");
            }

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<HealthcareOrganization>>(json);
        }

        private async Task<Dictionary<string, List<HealthcareOrganization>>> CreateScenariosAsync(List<HealthcareOrganization> organizations)
        {
            var scenarios = new Dictionary<string, List<HealthcareOrganization>>();
            
            // Сценарий 1: Все организации
            scenarios["all"] = organizations;

            // Сценарий 2: Только городские организации
            scenarios["urban"] = organizations.Where(o => IsUrbanOrganization(o)).ToList();

            // Сценарий 3: Только сельские организации
            scenarios["rural"] = organizations.Where(o => !IsUrbanOrganization(o)).ToList();

            // Сценарий 4: Организации с расстоянием до ЦСМ > 10 км
            scenarios["remote_csm"] = organizations.Where(o => o.DistanceToCsm > 10).ToList();

            // Сценарий 5: Организации с расстоянием до ГСВ > 5 км
            scenarios["remote_gsv"] = organizations.Where(o => o.DistanceToGsv > 5).ToList();

            // Сценарий 6: Организации с расстоянием до ФАП > 3 км
            scenarios["remote_fap"] = organizations.Where(o => o.DistanceToFap > 3).ToList();

            return scenarios;
        }

        private bool IsUrbanOrganization(HealthcareOrganization org)
        {
            // Определение городской организации на основе названия населенного пункта
            var urbanKeywords = new[] { "город", "г.", "городок", "поселок городского типа", "пгт" };
            return urbanKeywords.Any(keyword => 
                org.Np.ToLower().Contains(keyword.ToLower()));
        }

        private async Task SaveScenariosAsync(Dictionary<string, List<HealthcareOrganization>> scenarios)
        {
            foreach (var scenario in scenarios)
            {
                var filePath = Path.Combine(_workingPath, $"geo_scenario_{scenario.Key}.json");
                var json = JsonSerializer.Serialize(scenario.Value, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(filePath, json);
                _logger.LogInformation($"Сценарий {scenario.Key} сохранен в файл {filePath}");
            }
        }
    }
} 