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
            _logger.LogInformation("Начало расчета коэффициентов рабочей нагрузки");

            // Проверка входных данных
            if (mpop == null || fpop == null || mcoeff == null || fcoeff == null || k0 == null)
            {
                _logger.LogError("Один или несколько входных параметров равны null");
                throw new ArgumentNullException("Отсутствуют необходимые входные данные");
            }

            _logger.LogInformation($"Размеры входных данных: mpop={mpop.Count}, fpop={fpop.Count}, mcoeff={mcoeff.Count}, fcoeff={fcoeff.Count}, k0={k0.Count}");

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

            _logger.LogInformation($"Найдено {scmCodes.Count} уникальных кодов организаций");
            
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
                        var population = mpop[age][code];
                        var coefficient = mcoeff[age];
                        workloadValue += population * coefficient;
                        peopleValue += population * k0[age];

                        if (population * coefficient < 0)
                        {
                            _logger.LogWarning($"Отрицательное значение нагрузки для мужчин: код={code}, возраст={age}, население={population}, коэффициент={coefficient}");
                        }
                    }
                    
                    // Расчет для женщин
                    if (fpop.ContainsKey(age) && fpop[age].ContainsKey(code) && fcoeff.ContainsKey(age))
                    {
                        var population = fpop[age][code];
                        var coefficient = fcoeff[age];
                        workloadValue += population * coefficient;
                        peopleValue += population * k0[age];

                        if (population * coefficient < 0)
                        {
                            _logger.LogWarning($"Отрицательное значение нагрузки для женщин: код={code}, возраст={age}, население={population}, коэффициент={coefficient}");
                        }
                    }
                }
                
                // Расчет коэффициента нагрузки
                double workloadCoefficient = (peopleValue > 0) ? workloadValue / peopleValue : 0;

                if (workloadCoefficient <= 0)
                {
                    _logger.LogWarning($"Нулевой или отрицательный коэффициент нагрузки для организации {code}: workload={workloadValue}, people={peopleValue}, coefficient={workloadCoefficient}");
                }
                
                workloadResult[code] = new WorkloadData
                {
                    Scm_NewCode = code,
                    Workload = workloadValue,
                    People = peopleValue,
                    WorkloadCoefficient = workloadCoefficient
                };
            }

            _logger.LogInformation($"Расчет завершен. Обработано {workloadResult.Count} организаций");
            
            return workloadResult;
        }
        
        private void ApplyWorkloadLimits(Dictionary<int, WorkloadData> workload, double upMax, double downMax)
        {
            _logger.LogInformation($"Применение ограничений на коэффициенты: upMax={upMax}, downMax={downMax}");

            if (workload == null || !workload.Any())
            {
                _logger.LogError("Данные о рабочей нагрузке пусты или отсутствуют");
                throw new ArgumentException("Отсутствуют данные для применения ограничений", nameof(workload));
            }

            int adjustedUp = 0;
            int adjustedDown = 0;
            int unchanged = 0;

            foreach (var item in workload.Values)
            {
                item.Max = upMax;
                item.Min = downMax;
                
                var originalCoefficient = item.WorkloadCoefficient;
                
                // Применение ограничений на коэффициент нагрузки
                double adjustedCoefficient = Math.Min(originalCoefficient, upMax);
                adjustedCoefficient = Math.Max(adjustedCoefficient, downMax);
                
                item.AdjustedWorkloadCoefficient = adjustedCoefficient;

                // Подсчет статистики изменений
                if (adjustedCoefficient < originalCoefficient)
                {
                    adjustedDown++;
                    _logger.LogInformation($"Коэффициент уменьшен для {item.NewName}: {originalCoefficient:F2} -> {adjustedCoefficient:F2}");
                }
                else if (adjustedCoefficient > originalCoefficient)
                {
                    adjustedUp++;
                    _logger.LogInformation($"Коэффициент увеличен для {item.NewName}: {originalCoefficient:F2} -> {adjustedCoefficient:F2}");
                }
                else
                {
                    unchanged++;
                }
            }

            _logger.LogInformation($"Статистика применения ограничений:");
            _logger.LogInformation($"- Увеличено коэффициентов: {adjustedUp}");
            _logger.LogInformation($"- Уменьшено коэффициентов: {adjustedDown}");
            _logger.LogInformation($"- Осталось без изменений: {unchanged}");
            _logger.LogInformation($"Всего обработано: {workload.Count} организаций");
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
        
        private async Task SaveDataAsync(Dictionary<int, WorkloadData> data, string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            _logger.LogInformation($"Сохранение данных в: {filePath}");

            if (data == null || !data.Any())
            {
                _logger.LogError("Попытка сохранить пустые данные");
                throw new ArgumentException("Данные для сохранения отсутствуют", nameof(data));
            }

            _logger.LogInformation($"Количество записей для сохранения: {data.Count}");

            // Проверка данных перед сохранением
            foreach (var kvp in data)
            {
                if (kvp.Value == null)
                {
                    _logger.LogWarning($"Пустое значение для ключа {kvp.Key}");
                    continue;
                }

                if (kvp.Value.WorkloadCoefficient <= 0)
                {
                    _logger.LogWarning($"Нулевой или отрицательный коэффициент для организации {kvp.Value.NewName} (код {kvp.Key}): {kvp.Value.WorkloadCoefficient}");
                }

                if (kvp.Value.AdjustedWorkloadCoefficient <= 0)
                {
                    _logger.LogWarning($"Нулевой или отрицательный скорректированный коэффициент для организации {kvp.Value.NewName} (код {kvp.Key}): {kvp.Value.AdjustedWorkloadCoefficient}");
                }
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(data, options);
                await File.WriteAllTextAsync(filePath, json);
                _logger.LogInformation($"Данные успешно сохранены в файл {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении данных");
                throw;
            }
        }
        
        private async Task SaveParameterAsync(double value, string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            _logger.LogInformation($"Сохранение параметра {fileName}: {value}");

            try
            {
                var json = JsonSerializer.Serialize(value);
                await File.WriteAllTextAsync(filePath, json);
                _logger.LogInformation($"Параметр успешно сохранен в файл {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при сохранении параметра {fileName}");
                throw;
            }
        }
    }
}