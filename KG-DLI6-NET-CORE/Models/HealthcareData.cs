// Models/HealthcareData.cs
namespace KG_DLI6_NET_CORE.Models
{
    public class PopulationVisitData
    {
        public string Region { get; set; }
        public string Raion { get; set; }
        public long? SoateRegion { get; set; }
        public long? Soate_Raion { get; set; }
        public int Scm_NewCode { get; set; }
        public int? Scm_Code { get; set; }
        public string NewName { get; set; }
        public string FullName { get; set; }
        public int Age { get; set; }
        public int Total { get; set; }
        public int Men { get; set; }
        public int Women { get; set; }
        public int VisitTotal { get; set; }
        public int VisitMen { get; set; }
        public int VisitWomen { get; set; }
        public int mhi { get; set; }
    }

    public class HcoData
    {
        public string Region { get; set; }
        public string Raion { get; set; }
        public long? SoateRegion { get; set; }
        public long? Soate_Raion { get; set; }
        public int Scm_NewCode { get; set; }
        public int? Scm_Code { get; set; }
        public string NewName { get; set; }
        public string FullName { get; set; }
        public int nr_mhi { get; set; }
    }
    
    public class WorkloadData
    {
        public int Scm_NewCode { get; set; }
        public string Region { get; set; }
        public string Raion { get; set; }
        public long? SoateRegion { get; set; }
        public long? Soate_Raion { get; set; }
        public int? Scm_Code { get; set; }
        public string NewName { get; set; }
        public string FullName { get; set; }
        public double nr_mhi { get; set; }
            
        public double Workload { get; set; }
        public double People { get; set; }
        public double WorkloadCoefficient { get; set; }
        public double AdjustedWorkloadCoefficient { get; set; }
        public double Max { get; set; }
        public double Min { get; set; }
    }
   
    public class MergedHcoData
    {
        public double adjworkloadk { get; set; }

        // Базовые поля из WorkloadData
        public int Scm_NewCode { get; set; }
        public string Region { get; set; }
        public string Raion { get; set; }
        public long? SoateRegion { get; set; }
        public long? Soate_Raion { get; set; }
        public int? Scm_Code { get; set; }
        public string NewName { get; set; }
        public string FullName { get; set; }
        public double nr_mhi { get; set; }
        public double Workload { get; set; }
        public double People { get; set; }
        public double WorkloadCoefficient { get; set; }
        public double AdjustedWorkloadCoefficient { get; set; }
        public double Max { get; set; }
        public double Min { get; set; }
        
        // Поля из DensityData
        public double Altitude { get; set; }
        public double Density { get; set; }
        public double Rural { get; set; }
        public double Smalltown { get; set; }
        
        // Поля из TransferData
        public double? adj_originHO1 { get; set; }
        public double? adj_originHO2 { get; set; }
        public double? adj_destination { get; set; }
        
        // Поля из GeoBudgetData
        public double? budget_2023 { get; set; }
        public double? ЦСМ_ГСВ_2023 { get; set; }
        public double? geok_old_gsv { get; set; }
        public double? Все_нас_2023 { get; set; }
        
        // Рассчитываемые поля
        public double geok_old_ns { get; set; }
        public double? geok_old { get; set; }
        public double altitude { get; set; }
        public double rural { get; set; }
        public double density { get; set; }
    }
    
    public class DensityData
    {
        public long? Soate_Raion { get; set; }
        public double Altitude { get; set; }
        public double Density { get; set; }
        public double Rural { get; set; }
        public double Smalltown { get; set; }
    }
    
    public class TransferData
    {
        public int? Scm_NewCode { get; set; }
        public double? adj_originHO1 { get; set; }
        public double? adj_originHO2 { get; set; }
        public double? adj_destination { get; set; }
    }
    
    public class GeoBudgetData
    {
        public int? Scm_NewCode { get; set; }
        public double? budget_2023 { get; set; }
        public double? ЦСМ_ГСВ_2023 { get; set; }
        public double? geok_old_gsv { get; set; }
        public double? Все_нас_2023 { get; set; }
    }
    
    public class BudgetReplicationData
    {
        // Основные идентификаторы
        public int Scm_NewCode { get; set; }
        public string Region { get; set; }
        public string Raion { get; set; }
        public string SoateRegion { get; set; }
        public string Soate_Raion { get; set; }
        public string Scm_Code { get; set; }
        public string NewName { get; set; }
        public string FullName { get; set; }
        
        // Данные о населении
        public double nr_mhi { get; set; }  // Количество застрахованных
        public double People { get; set; }  // Общее количество людей
        public double people_NS { get; set; } // Население для узких специалистов
        
        // Географические коэффициенты
        public double geok_old_ns { get; set; }  // Старый географический коэффициент для узких специалистов
        public double geok_old_gsv { get; set; } // Старый географический коэффициент для семейной медицины
        public double geok_old { get; set; }     // Общий старый географический коэффициент
        
        // Бюджетные данные
        public double budget_2023 { get; set; }   // Бюджет 2023
        public double ЦСМ_ГСВ_2023 { get; set; }  // Бюджет центров семейной медицины 2023
        public double Все_нас_2023 { get; set; }  // Всё население 2023
        public double застрах_нас_2023 { get; set; } // Застрахованное население 2023
        public double не_застрах_нас_2023 { get; set; } // Незастрахованное население 2023
        
        // Коэффициенты расчета
        public double i_u_ratio { get; set; }  // Соотношение застрахованных/незастрахованных
        public double prefk { get; set; }      // Коэффициент предпочтения
        
        // Расчетные бюджеты
        public double budget_repl_1 { get; set; }   // Бюджет узких специалистов (воспроизведенный)
        public double budget_repl_2 { get; set; }   // Бюджет семейной медицины (воспроизведенный)
        public double budget_repl_tot { get; set; } // Общий воспроизведенный бюджет
        
        // Отклонения
        public double budget_repl { get; set; }  // Отклонение воспроизведенного бюджета от фактического
        public double ins_repl { get; set; }     // Отклонение воспроизведенного застрахованного населения от фактического
    }
    
    public class GeoData
    {
        // Данные об области
        public string oblast_name { get; set; } = string.Empty;
        public string oblast_soate { get; set; } = string.Empty;
        
        // Данные о районе
        public string rayon_name { get; set; } = string.Empty;
        public string rayon_soate { get; set; } = string.Empty;
        public double rayon_km_oblast { get; set; } // Расстояние райцентра от облцентра
        
        // Данные о населенном пункте
        public string np_soate { get; set; } = string.Empty;
        public string np_name { get; set; } = string.Empty;
        public double np_pop { get; set; } // Численность населения
        public double np_km_rayon { get; set; } // Расстояние до райцентра
        public double np_geocoeff { get; set; } // Сельский коэффициент
    }
    
    public class RayonGeoCoefficient
    {
        public float rayon_soate { get; set; }
        public double rayon_geocoeff_w { get; set; } // Взвешенный географический коэффициент района
    }
    
    public class SimulationData
    {
        // Идентификаторы организации
        public int Scm_NewCode { get; set; }
        public string Region { get; set; } = string.Empty;
        public string NewName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        
        // Данные о населении и нагрузке
        public double people { get; set; }
        public double adjworkloadk { get; set; }
        
        // Географические коэффициенты
        public double geok_old { get; set; }
        public double geok_1 { get; set; }
        public double geok_2 { get; set; }
        public double geok_3 { get; set; }
        
        // Параметры для расчета географических коэффициентов
        public double altitude { get; set; }
        public double rural { get; set; }
        public double density { get; set; }
        
        // Бюджетные данные
        public double ЦСМ_ГСВ_2023 { get; set; }
        
        // Связи с другими организациями для корректировки узких специалистов
        public double? adj_originHO1 { get; set; }
        public double? adj_originHO2 { get; set; }
        public double? adj_destination { get; set; }
        
        // Динамические поля для расчетных данных
        private readonly Dictionary<string, double> _dynamicFields = new Dictionary<string, double>();
        
        // Свойства для отображения основных рассчитанных значений в API
        // (эти свойства извлекают значения из динамических полей)
        public double budget_raw_geok_old => GetRawBudget("budget_raw_geok_old");
        public double budget_raw_geok_1 => GetRawBudget("budget_raw_geok_1");
        public double budget_new_geok_old => GetNewBudget("budget_new_geok_old");
        public double budget_new_geok_1 => GetNewBudget("budget_new_geok_1");
        public double impact_geok_old => GetImpact("impact_geok_old");
        public double impact_geok_1 => GetImpact("impact_geok_1");
        
        // Методы для работы с динамическими полями
        public void SetRawBudget(string fieldName, double value)
        {
            _dynamicFields[fieldName] = value;
        }
        
        public double GetRawBudget(string fieldName)
        {
            return _dynamicFields.TryGetValue(fieldName, out var value) ? value : 0;
        }
        
        public void SetCorrectionAdd1(string fieldName, double value)
        {
            _dynamicFields[fieldName] = value;
        }
        
        public double GetCorrectionAdd1(string fieldName)
        {
            return _dynamicFields.TryGetValue(fieldName, out var value) ? value : 0;
        }
        
        public void SetCorrectionAdd2(string fieldName, double value)
        {
            _dynamicFields[fieldName] = value;
        }
        
        public double GetCorrectionAdd2(string fieldName)
        {
            return _dynamicFields.TryGetValue(fieldName, out var value) ? value : 0;
        }
        
        public void SetCorrectionSubtr(string fieldName, double value)
        {
            _dynamicFields[fieldName] = value;
        }
        
        public double GetCorrectionSubtr(string fieldName)
        {
            return _dynamicFields.TryGetValue(fieldName, out var value) ? value : 0;
        }
        
        public void SetNewBudget(string fieldName, double value)
        {
            _dynamicFields[fieldName] = value;
        }
        
        public double GetNewBudget(string fieldName)
        {
            return _dynamicFields.TryGetValue(fieldName, out var value) ? value : 0;
        }
        
        public void SetImpact(string fieldName, double value)
        {
            _dynamicFields[fieldName] = value;
        }
        
        public double GetImpact(string fieldName)
        {
            return _dynamicFields.TryGetValue(fieldName, out var value) ? value : 0;
        }
    }
    
    public class HealthcareOrganization
    {
        public string Oblast { get; set; }
        public string NpSoate { get; set; }
        public string Np { get; set; }
        public int NpPopulation { get; set; }
        public string NpCoordinates { get; set; }
        public string CsmCode { get; set; }
        public string Csm { get; set; }
        public string CsmAddress { get; set; }
        public string CsmCoordinates { get; set; }
        public string CsmLocation { get; set; }
        public string GsvCode { get; set; }
        public string Gsv { get; set; }
        public string GsvAddress { get; set; }
        public string GsvCoordinates { get; set; }
        public string GsvLocation { get; set; }
        public string FapCode { get; set; }
        public string Fap { get; set; }
        public string FapAddress { get; set; }
        public string FapCoordinates { get; set; }
        public string FapLocation { get; set; }
        public int RowNumber { get; set; }
        public int NFap { get; set; }
        public int NGsv { get; set; }
        public List<string> FapCodes { get; set; }
        public List<string> FapNames { get; set; }
        public List<string> FapCoordinatesList { get; set; }
        public List<string> GsvCodes { get; set; }
        public List<string> GsvNames { get; set; }
        public List<string> GsvCoordinatesList { get; set; }
        
        public double? DistanceToCsm { get; set; }
        public double? DistanceToGsv { get; set; }
        public double? DistanceToFap { get; set; }
    }
    
    public class CensusData
    {
        public string SoateNp { get; set; }
        public string Name { get; set; }
        public int Population { get; set; }
        public string Type { get; set; }
        public string SoateOblast { get; set; }
        public string SoateRayon { get; set; }
        public string SoateAimak { get; set; }
        public string Oblast { get; set; }
        public string Rayon { get; set; }
        public string Aimak { get; set; }
        public string CorrectedName { get; set; }
        public double Correction { get; set; }
        public int CorrectedPopulation { get; set; }
    }
    
    public class HealthFacilityData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Address { get; set; }
        public string Region { get; set; }
        public string District { get; set; }
        public string Village { get; set; }
        public string Coordinates { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string Status { get; set; }
        public string Ownership { get; set; }
        public string Services { get; set; }
        public string Staff { get; set; }
        public string Equipment { get; set; }
        public string Notes { get; set; }
    }
    
    public class PopulationData
    {
        public string AgeGroup { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
    }
    
    public class VisitData
    {
        public string AgeGroup { get; set; }
        public int MaleVisits { get; set; }
        public int FemaleVisits { get; set; }
    }
    
    public class AgeSexCoefficient
    {
        public string Age { get; set; }
        public string Sex { get; set; }
        public double Coefficient { get; set; }
    }
}