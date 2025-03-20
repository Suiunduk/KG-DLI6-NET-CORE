using System.Text.Json;
using ClosedXML.Excel;
using KG_DLI6_NET_CORE.Models;

namespace KG_DLI6_NET_CORE.Services
{
    public class HcoMergeService
    {
        private readonly ILogger<HcoMergeService> _logger;
        private readonly string _workingPath;
        private readonly string _originalPath;
        private readonly string _outputPath;

        public HcoMergeService(ILogger<HcoMergeService> logger)
        {
            _logger = logger;
            
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _originalPath = Path.Combine(Directory.GetCurrentDirectory(), "original");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
            
            // Создаем директории, если они не существуют
            Directory.CreateDirectory(_workingPath);
            Directory.CreateDirectory(_originalPath);
            Directory.CreateDirectory(_outputPath);
        }
        
        public async Task<Dictionary<int, MergedHcoData>> MergeHcoDataAsync()
        {
            _logger.LogInformation("Начало объединения данных о медицинских учреждениях");
            
            // Загрузка данных о рабочей нагрузке
            var workloadData = await LoadWorkloadDataAsync();
            _logger.LogInformation($"Загружены данные о рабочей нагрузке: {workloadData.Count} записей");
            
            // Загрузка данных о плотности населения районов
            var densityData = await LoadDensityDataAsync();
            _logger.LogInformation($"Загружены данные о плотности населения: {densityData.Count} записей");
            
            // Объединение данных о рабочей нагрузке с данными о плотности населения
            var mergedWithDensity = MergeWithDensity(workloadData, densityData);
            _logger.LogInformation($"Объединенные данные с плотностью: {mergedWithDensity.Count} записей");
            
            // Загрузка данных о трансферах
            var transfersData = await LoadTransfersDataAsync();
            _logger.LogInformation($"Загружены данные о трансферах: {transfersData.Count} записей");
            
            // Объединение с данными о трансферах
            var mergedWithTransfers = MergeWithTransfers(mergedWithDensity, transfersData);
            _logger.LogInformation($"Объединенные данные с трансферами: {mergedWithTransfers.Count} записей");
            
            // Загрузка данных о бюджете и геокоэффициентах
            var geoBudgetData = await LoadGeoBudgetDataAsync();
            _logger.LogInformation($"Загружены данные о бюджете и геокоэффициентах: {geoBudgetData.Count} записей");
            
            // Объединение с данными о бюджете и геокоэффициентах
            var finalMergedData = MergeWithGeoBudget(mergedWithTransfers, geoBudgetData);
            _logger.LogInformation($"Финальные объединенные данные: {finalMergedData.Count} записей");
            
            // Расчет старых географических коэффициентов
            CalculateOldGeoCoefficients(finalMergedData);
            
            // Сохранение объединенных данных
            await SaveMergedDataAsync(finalMergedData);
            
            _logger.LogInformation("Объединение данных о медицинских учреждениях завершено");
            
            return finalMergedData;
        }
        
        private async Task<Dictionary<int, WorkloadData>> LoadWorkloadDataAsync()
        {
            var filePath = Path.Combine(_workingPath, "workload");
            _logger.LogInformation($"Загрузка данных о рабочей нагрузке из: {filePath}");
            
            if (!File.Exists(filePath))
            {
                _logger.LogError($"Файл не найден: {filePath}");
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }
            
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<Dictionary<int, WorkloadData>>(json);
        }
        
        private async Task<List<DensityData>> LoadDensityDataAsync()
        {
            var filePath = Path.Combine(_originalPath, "density_raion.xlsx");
            _logger.LogInformation($"Загрузка данных о плотности населения из: {filePath}");
            
            if (!File.Exists(filePath))
            {
                _logger.LogError($"Файл не найден: {filePath}");
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }
            
            var densityData = new List<DensityData>();
            
            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet("Sheet1");
                var firstRow = true;
                var rowCount = worksheet.LastRowUsed().RowNumber();
                
                for (int row = 1; row <= rowCount; row++)
                {
                    // Пропускаем заголовок
                    if (firstRow)
                    {
                        firstRow = false;
                        continue;
                    }
                    
                    // Чтение данных из ячеек
                    long? soateRaion = worksheet.Cell(row, 1).GetValue<long?>();
                    var altitude = worksheet.Cell(row, 4).GetValue<double>();
                    var density = worksheet.Cell(row, 5).GetValue<double>();
                    var rural = worksheet.Cell(row, 6).GetValue<double>();
                    var smalltown = worksheet.Cell(row, 7).GetValue<double>();
                    
                    // В Python коде значение умножается на 1000
                    soateRaion *= 1000;
                    
                    densityData.Add(new DensityData
                    {
                        Soate_Raion = soateRaion,
                        Altitude = altitude,
                        Density = density,
                        Rural = rural,
                        Smalltown = smalltown
                    });
                }
            }
            
            return densityData;
        }
        
        private async Task<List<TransferData>> LoadTransfersDataAsync()
        {
            var filePath = Path.Combine(_originalPath, "transfers.xlsx");
            _logger.LogInformation($"Загрузка данных о трансферах из: {filePath}");
            
            if (!File.Exists(filePath))
            {
                _logger.LogError($"Файл не найден: {filePath}");
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }
            
            var transfersData = new List<TransferData>();
            
            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet("Sheet1");
                var firstRow = true;
                var rowCount = worksheet.LastRowUsed().RowNumber();
                
                for (int row = 1; row <= rowCount; row++)
                {
                    // Пропускаем заголовок
                    if (firstRow)
                    {
                        firstRow = false;
                        continue;
                    }
                    
                    // Чтение данных из ячеек
                    var scmNewCode = worksheet.Cell(row, 6).GetValue<int?>();
                    var adjOriginHO1 = worksheet.Cell(row, 8).GetValue<double?>();
                    var adjOriginHO2 = worksheet.Cell(row, 9).GetValue<double?>();
                    var adjDestination = worksheet.Cell(row, 10).GetValue<double?>();
                    
                    transfersData.Add(new TransferData
                    {
                        Scm_NewCode = scmNewCode,
                        adj_originHO1 = adjOriginHO1,
                        adj_originHO2 = adjOriginHO2,
                        adj_destination = adjDestination
                    });
                }
            }
            
            return transfersData;
        }
        
        private async Task<List<GeoBudgetData>> LoadGeoBudgetDataAsync()
        {
            var filePath = Path.Combine(_originalPath, "geo_budget_edited.xlsx");
            _logger.LogInformation($"Загрузка данных о бюджете и геокоэффициентах из: {filePath}");
            
            if (!File.Exists(filePath))
            {
                _logger.LogError($"Файл не найден: {filePath}");
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }
            
            var geoBudgetData = new List<GeoBudgetData>();
            
            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet("Sheet1");
                var firstRow = true;
                var rowCount = worksheet.LastRowUsed().RowNumber();
                
                for (int row = 1; row <= rowCount; row++)
                {
                    // Пропускаем заголовок
                    if (firstRow)
                    {
                        firstRow = false;
                        continue;
                    }
                    
                    // Чтение данных из ячеек
                    var scmNewCode = worksheet.Cell(row, 6).GetValue<int?>();
                    var budget2023 = worksheet.Cell(row, 12).GetValue<double?>();
                    var ЦСМ_ГСВ_2023 = worksheet.Cell(row, 13).GetValue<double?>();
                    var geo_old_gsv = worksheet.Cell(row, 29).GetValue<double?>();
                    var Все_нас_2023 = worksheet.Cell(row, 19).GetValue<double?>();
                    
                    // Пропускаем организации, у которых нет бюджета ПМП
                    if (ЦСМ_ГСВ_2023 == 0)
                        continue;
                    
                    geoBudgetData.Add(new GeoBudgetData
                    {
                        Scm_NewCode = scmNewCode,
                        budget_2023 = budget2023,
                        ЦСМ_ГСВ_2023 = ЦСМ_ГСВ_2023,
                        geok_old_gsv = geo_old_gsv,
                        Все_нас_2023 = Все_нас_2023
                    });
                }
            }
            
            return geoBudgetData;
        }
        
        private Dictionary<int, MergedHcoData> MergeWithDensity(Dictionary<int, WorkloadData> workloadData, List<DensityData> densityData)
        {
            var result = new Dictionary<int, MergedHcoData>();
            
            // Создаем словарь для быстрого доступа к данным о плотности по Soate_Raion
            Dictionary<long?, DensityData> densityByRaion = densityData.ToDictionary(d => d.Soate_Raion);
            
            // Объединяем данные
            foreach (var item in workloadData)
            {
                var mergedItem = new MergedHcoData
                {
                    Scm_NewCode = item.Key,
                    Region = item.Value.Region,
                    Raion = item.Value.Raion,
                    SoateRegion = item.Value.SoateRegion,
                    Soate_Raion = item.Value.Soate_Raion,
                    Scm_Code = item.Value.Scm_Code,
                    NewName = item.Value.NewName,
                    FullName = item.Value.FullName,
                    nr_mhi = item.Value.nr_mhi,
                    Workload = item.Value.Workload,
                    People = item.Value.People,
                    WorkloadCoefficient = item.Value.WorkloadCoefficient,
                    AdjustedWorkloadCoefficient = item.Value.AdjustedWorkloadCoefficient,
                    Max = item.Value.Max,
                    Min = item.Value.Min
                };
                
                // Добавляем данные о плотности, если они существуют
                if (densityByRaion.TryGetValue(item.Value.Soate_Raion, out var density))
                {
                    mergedItem.Altitude = density.Altitude;
                    mergedItem.Density = density.Density;
                    mergedItem.Rural = density.Rural;
                    mergedItem.Smalltown = density.Smalltown;
                }
                
                result[item.Key] = mergedItem;
            }
            
            return result;
        }
        
        private Dictionary<int, MergedHcoData> MergeWithTransfers(
            Dictionary<int, MergedHcoData> mergedData, 
            List<TransferData> transfersData)
        {
            // Создаем словарь для быстрого доступа к данным о трансферах по Scm_NewCode
            var transfersByCode = transfersData.ToDictionary(t => t.Scm_NewCode);
            
            // Объединяем данные
            foreach (var item in mergedData.Values)
            {
                if (transfersByCode.TryGetValue(item.Scm_NewCode, out var transfer))
                {
                    item.adj_originHO1 = transfer.adj_originHO1;
                    item.adj_originHO2 = transfer.adj_originHO2;
                    item.adj_destination = transfer.adj_destination;
                }
            }
            
            return mergedData;
        }
        
        private Dictionary<int, MergedHcoData> MergeWithGeoBudget(
            Dictionary<int, MergedHcoData> mergedData, 
            List<GeoBudgetData> geoBudgetData)
        {
            var result = new Dictionary<int, MergedHcoData>();
            
            // Создаем словарь для быстрого доступа к данным о бюджете по Scm_NewCode
            var budgetByCode = geoBudgetData.ToDictionary(g => g.Scm_NewCode);
            
            // Объединяем данные (только организации, которые есть в обоих наборах данных)
            foreach (var item in mergedData.Values)
            {
                if (budgetByCode.TryGetValue(item.Scm_NewCode, out var budget))
                {
                    item.budget_2023 = budget.budget_2023;
                    item.ЦСМ_ГСВ_2023 = budget.ЦСМ_ГСВ_2023;
                    item.geok_old_gsv = budget.geok_old_gsv;
                    item.Все_нас_2023 = budget.Все_нас_2023;
                    
                    result[item.Scm_NewCode] = item;
                }
            }
            
            return result;
        }
        
        private void CalculateOldGeoCoefficients(Dictionary<int, MergedHcoData> mergedData)
        {
            // Вычисление старого географического коэффициента для узких специалистов
            // geok_old_ns = altitude + smalltown + rural - 2
            double? totalPopulation = 0;
            double? weightedSumNs = 0;
            double? weightedSumGsv = 0;
            
            foreach (var item in mergedData.Values)
            {
                // Расчет коэффициента для узких специалистов
                item.geok_old_ns = item.Altitude + item.Smalltown + item.Rural - 2;
                
                // Расчет общего коэффициента (приближение)
                item.geok_old = item.geok_old_gsv * 0.75 + item.geok_old_ns * 0.25;
                
                // Суммирование для взвешенного среднего
                if (item.Все_нас_2023 > 0)
                {
                    totalPopulation += item.Все_нас_2023;
                    weightedSumNs += item.geok_old_ns * item.Все_нас_2023;
                    weightedSumGsv += item.geok_old_gsv * item.Все_нас_2023;
                }
            }
            
            // Расчет взвешенного среднего для коэффициентов
            var geok_old_ns_w_avg = weightedSumNs / totalPopulation;
            var geok_old_gsv_w_avg = weightedSumGsv / totalPopulation;
            
            _logger.LogInformation($"Взвешенное среднее старого географического коэффициента для узких специалистов: {geok_old_ns_w_avg}");
            _logger.LogInformation($"Взвешенное среднее старого географического коэффициента для семейной медицины: {geok_old_gsv_w_avg}");
        }
        
        private async Task SaveMergedDataAsync(Dictionary<int, MergedHcoData> mergedData)
        {
            var filePath = Path.Combine(_workingPath, "merged_df");
            _logger.LogInformation($"Сохранение объединенных данных в: {filePath}");
            
            string json = JsonSerializer.Serialize(mergedData);
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInformation("Объединенные данные успешно сохранены");
        }
    }
}