using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using KG_DLI6_NET_CORE.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KG_DLI6_NET_CORE.Services
{
    public class DistanceCalculationService
    {
        private readonly ILogger<DistanceCalculationService> _logger;
        private readonly string _workingPath;
        private readonly string _outputPath;
        private readonly HttpClient _httpClient;
        private readonly string _googleMapsApiKey;

        public DistanceCalculationService(
            ILogger<DistanceCalculationService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _workingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "working");
            _outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            _httpClient = new HttpClient();
            _googleMapsApiKey = configuration["GoogleMaps:ApiKey"];

            if (!Directory.Exists(_workingPath))
                Directory.CreateDirectory(_workingPath);
            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);
        }

        public async Task<List<HealthcareOrganization>> CalculateDistancesAsync()
        {
            _logger.LogInformation("Начало расчета расстояний между медицинскими организациями");

            try
            {
                // Загрузка данных
                var organizations = await LoadOrganizationsAsync();
                
                // Расчет расстояний
                await CalculateDistancesForOrganizationsAsync(organizations);
                
                // Сохранение результатов
                await SaveResultsAsync(organizations);
                
                _logger.LogInformation($"Завершен расчет расстояний для {organizations.Count} медицинских организаций");
                return organizations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при расчете расстояний");
                throw;
            }
        }

        private async Task<List<HealthcareOrganization>> LoadOrganizationsAsync()
        {
            var filePath = Path.Combine(_workingPath, "enriched_healthcare_organizations.json");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Файл с обогащенными данными о медицинских организациях не найден");
            }

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<HealthcareOrganization>>(json);
        }

        private async Task CalculateDistancesForOrganizationsAsync(List<HealthcareOrganization> organizations)
        {
            foreach (var org in organizations)
            {
                if (!string.IsNullOrEmpty(org.NpCoordinates) && !string.IsNullOrEmpty(org.CsmCoordinates))
                {
                    var (npLat, npLon) = ParseCoordinates(org.NpCoordinates);
                    var (csmLat, csmLon) = ParseCoordinates(org.CsmCoordinates);
                    
                    if (npLat.HasValue && npLon.HasValue && csmLat.HasValue && csmLon.HasValue)
                    {
                        org.DistanceToCsm = await CalculateDrivingDistanceAsync(
                            npLat.Value, npLon.Value, csmLat.Value, csmLon.Value);
                    }
                }

                if (!string.IsNullOrEmpty(org.NpCoordinates) && !string.IsNullOrEmpty(org.GsvCoordinates))
                {
                    var (npLat, npLon) = ParseCoordinates(org.NpCoordinates);
                    var (gsvLat, gsvLon) = ParseCoordinates(org.GsvCoordinates);
                    
                    if (npLat.HasValue && npLon.HasValue && gsvLat.HasValue && gsvLon.HasValue)
                    {
                        org.DistanceToGsv = await CalculateDrivingDistanceAsync(
                            npLat.Value, npLon.Value, gsvLat.Value, gsvLon.Value);
                    }
                }

                if (!string.IsNullOrEmpty(org.NpCoordinates) && !string.IsNullOrEmpty(org.FapCoordinates))
                {
                    var (npLat, npLon) = ParseCoordinates(org.NpCoordinates);
                    var (fapLat, fapLon) = ParseCoordinates(org.FapCoordinates);
                    
                    if (npLat.HasValue && npLon.HasValue && fapLat.HasValue && fapLon.HasValue)
                    {
                        org.DistanceToFap = await CalculateDrivingDistanceAsync(
                            npLat.Value, npLon.Value, fapLat.Value, fapLon.Value);
                    }
                }
            }
        }

        private (double? latitude, double? longitude) ParseCoordinates(string coordinates)
        {
            try
            {
                // Поддержка различных форматов координат
                if (coordinates.Contains("°"))
                {
                    // Формат: 41°51'45.7 N 72°56'48.3 E
                    var parts = coordinates.Split(' ');
                    if (parts.Length == 4)
                    {
                        var latParts = parts[0].Split('°', '\'', '"');
                        var lonParts = parts[2].Split('°', '\'', '"');
                        
                        var lat = double.Parse(latParts[0]) + 
                                 double.Parse(latParts[1]) / 60 + 
                                 double.Parse(latParts[2]) / 3600;
                        var lon = double.Parse(lonParts[0]) + 
                                 double.Parse(lonParts[1]) / 60 + 
                                 double.Parse(lonParts[2]) / 3600;
                        
                        if (parts[1] == "S") lat = -lat;
                        if (parts[3] == "W") lon = -lon;
                        
                        return (lat, lon);
                    }
                }
                else
                {
                    // Формат: 40.381417, 72.978076
                    var parts = coordinates.Split(',');
                    if (parts.Length == 2)
                    {
                        return (double.Parse(parts[0].Trim()), double.Parse(parts[1].Trim()));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Ошибка при разборе координат: {coordinates}");
            }
            
            return (null, null);
        }

        private async Task<double> CalculateDrivingDistanceAsync(double lat1, double lon1, double lat2, double lon2)
        {
            try
            {
                var url = $"https://maps.googleapis.com/maps/api/distancematrix/json?" +
                         $"origins={lat1},{lon1}&destinations={lat2},{lon2}&mode=driving&key={_googleMapsApiKey}";
                
                var response = await _httpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<GoogleMapsResponse>(response);
                
                if (data?.Status == "OK" && 
                    data.Rows?.FirstOrDefault()?.Elements?.FirstOrDefault()?.Status == "OK")
                {
                    return data.Rows[0].Elements[0].Distance.Value / 1000.0; // Конвертация в километры
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при расчете расстояния через Google Maps API");
            }
            
            // В случае ошибки используем формулу гаверсинусов
            return CalculateHaversineDistance(lat1, lon1, lat2, lon2);
        }

        private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371.0; // Радиус Земли в километрах
            
            var lat1Rad = ToRadians(lat1);
            var lon1Rad = ToRadians(lon1);
            var lat2Rad = ToRadians(lat2);
            var lon2Rad = ToRadians(lon2);
            
            var dLat = lat2Rad - lat1Rad;
            var dLon = lon2Rad - lon1Rad;
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private async Task SaveResultsAsync(List<HealthcareOrganization> organizations)
        {
            var filePath = Path.Combine(_workingPath, "organizations_with_distances.json");
            var json = JsonSerializer.Serialize(organizations, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation($"Результаты сохранены в файл {filePath}");
        }
    }

    public class GoogleMapsResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("rows")]
        public List<Row> Rows { get; set; }
    }

    public class Row
    {
        [JsonPropertyName("elements")]
        public List<Element> Elements { get; set; }
    }

    public class Element
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("distance")]
        public Distance Distance { get; set; }
    }

    public class Distance
    {
        [JsonPropertyName("value")]
        public int Value { get; set; }
    }
} 