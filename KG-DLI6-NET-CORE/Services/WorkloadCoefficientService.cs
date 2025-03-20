using System.Text.Json;
using KG_DLI6_NET_CORE.Models;

namespace KG_DLI6_NET_CORE.Services
{
    public class WorkloadCoefficientService
    {
        private readonly ILogger<WorkloadCoefficientService> _logger;
        private readonly string _workingPath;
        private readonly string _outputPath;
        
        // Параметры по умолчанию для коэффициентов
        private readonly double _defaultUpMax = 1.30;
        private readonly double _defaultDownMax = 0.70;

        public WorkloadCoefficientService(ILogger<WorkloadCoefficientService> logger)
        {
            _logger = logger;
            
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
            
            // Создаем директории, если они не существуют
            Directory.CreateDirectory(_workingPath);
            Directory.CreateDirectory(_outputPath);
        }
        
        public async Task<Dictionary<int, WorkloadData>> CalculateWorkloadCoefficientsAsync(
            double? upMax = null, 
            double? downMax = null)
        {
            _logger.LogInformation("Начало расчета коэффициентов нагрузки");
            
            // Установка параметров
            double actualUpMax = upMax ?? _defaultUpMax;
            double actualDownMax = downMax ?? _defaultDownMax;
            
            // Проверка диапазонов
            if (actualUpMax > 1.25 || actualUpMax < 1)
            {
                _logger.LogWarning("Максимально допустимый коэффициент рабочей нагрузки не находится в требуемом диапазоне. Установлен на 1.25.");
                actualUpMax = 1.25;
            }
            
            if (actualDownMax > 1 || actualDownMax < 0.75)
            {
                _logger.LogWarning("Минимально допустимый коэффициент рабочей нагрузки не находится в требуемом диапазоне. Установлен на 0.75.");
                actualDownMax = 0.75;
            }
            
            // Загрузка данных
            var mpop = await LoadDataAsync<Dictionary<int, Dictionary<int, double>>>("mpop");
            var fpop = await LoadDataAsync<Dictionary<int, Dictionary<int, double>>>("fpop");
            var mcoeff = await LoadDataAsync<Dictionary<int, double>>("mcoeff");
            var fcoeff = await LoadDataAsync<Dictionary<int, double>>("fcoeff");
            var hcoList = await LoadDataAsync<List<HcoData>>("hco_list");
            
            // Создание статус-кво коэффициентов (K0)
            var k0 = new Dictionary<int, double>();
            for (int i = 0; i < 100; i++)
            {
                k0[i] = 1.0;
            }
            
            // Расчет нагрузки и количества людей
            var workload = CalculateWorkload(mpop, fpop, mcoeff, fcoeff, k0);
            
            // Применение ограничений на коэффициенты нагрузки
            ApplyWorkloadLimits(workload, actualUpMax, actualDownMax);
            
            // Объединение с информацией о медицинских учреждениях
            MergeWithHcoData(workload, hcoList);
            
            // Сохранение данных
            await SaveDataAsync(workload, "workload");
            await SaveParameterAsync(actualUpMax, "upmax_value");
            await SaveParameterAsync(actualDownMax, "downmax_value");
            
            _logger.LogInformation($"Расчет коэффициентов нагрузки завершен. Количество ОЗ: {workload.Count}");
            
            return workload;
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
        
        private Dictionary<int, WorkloadData> CalculateWorkload(
            Dictionary<int, Dictionary<int, double>> mpop,
            Dictionary<int, Dictionary<int, double>> fpop,
            Dictionary<int, double> mcoeff,
            Dictionary<int, double> fcoeff,
            Dictionary<int, double> k0)
        {
            var workloadResult = new Dictionary<int, WorkloadData>();
            
            // Получение списка Scm_NewCode (ключей для учреждений)
            var scmCodes = new HashSet<int>();
            foreach (var age in mpop.Keys)
            {
                foreach (var code in mpop[age].Keys)
                {
                    scmCodes.Add(code);
                }
            }
            
            foreach (var age in fpop.Keys)
            {
                foreach (var code in fpop[age].Keys)
                {
                    scmCodes.Add(code);
                }
            }
            
            // Расчет нагрузки и количества людей
            foreach (var code in scmCodes)
            {
                double workloadValue = 0;
                double peopleValue = 0;
                
                for (int age = 0; age < 100; age++)
                {
                    // Расчет для мужчин
                    if (mpop.ContainsKey(age) && mpop[age].ContainsKey(code) && mcoeff.ContainsKey(age))
                    {
                        workloadValue += mpop[age][code] * mcoeff[age];
                        peopleValue += mpop[age][code] * k0[age];
                    }
                    
                    // Расчет для женщин
                    if (fpop.ContainsKey(age) && fpop[age].ContainsKey(code) && fcoeff.ContainsKey(age))
                    {
                        workloadValue += fpop[age][code] * fcoeff[age];
                        peopleValue += fpop[age][code] * k0[age];
                    }
                }
                
                // Расчет коэффициента нагрузки
                double workloadCoefficient = (peopleValue > 0) ? workloadValue / peopleValue : 0;
                
                workloadResult[code] = new WorkloadData
                {
                    Scm_NewCode = code,
                    Workload = workloadValue,
                    People = peopleValue,
                    WorkloadCoefficient = workloadCoefficient
                };
            }
            
            return workloadResult;
        }
        
        private void ApplyWorkloadLimits(Dictionary<int, WorkloadData> workload, double upMax, double downMax)
        {
            foreach (var item in workload.Values)
            {
                item.Max = upMax;
                item.Min = downMax;
                
                // Применение ограничений на коэффициент нагрузки
                double adjustedCoefficient = Math.Min(item.WorkloadCoefficient, upMax);
                adjustedCoefficient = Math.Max(adjustedCoefficient, downMax);
                
                item.AdjustedWorkloadCoefficient = adjustedCoefficient;
            }
        }
        
        private void MergeWithHcoData(Dictionary<int, WorkloadData> workload, List<HcoData> hcoList)
        {
            foreach (var hco in hcoList)
            {
                if (workload.ContainsKey(hco.Scm_NewCode))
                {
                    var workloadItem = workload[hco.Scm_NewCode];
                    
                    workloadItem.Region = hco.Region;
                    workloadItem.Raion = hco.Raion;
                    workloadItem.SoateRegion = hco.SoateRegion;
                    workloadItem.Soate_Raion = hco.Soate_Raion;
                    workloadItem.Scm_Code = hco.Scm_Code;
                    workloadItem.NewName = hco.NewName;
                    workloadItem.FullName = hco.FullName;
                    workloadItem.nr_mhi = hco.nr_mhi;
                    
                    // Исправление кодировки для конкретного учреждения (как в Python-коде)
                    if (hco.Scm_NewCode == 927181)
                    {
                        workloadItem.Raion = "город Ош";
                        workloadItem.Soate_Raion = 41721000000000000;
                    }
                }
            }
        }
        
        private async Task SaveDataAsync<T>(T data, string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            _logger.LogInformation($"Сохранение данных в: {filePath}");
            
            string json = JsonSerializer.Serialize(data);
            await File.WriteAllTextAsync(filePath, json);
        }
        
        private async Task SaveParameterAsync(double value, string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            _logger.LogInformation($"Сохранение параметра в: {filePath}");
            
            string json = JsonSerializer.Serialize(value);
            await File.WriteAllTextAsync(filePath, json);
        }
    }
}