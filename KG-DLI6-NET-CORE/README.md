# КГ-DLI6 - Система расчета подушевых нормативов для системы здравоохранения Кыргызстана

Данный проект представляет собой миграцию системы расчетов из Python в ASP.NET Core 6. Система предназначена для расчета подушевых нормативов и платежей, с учетом половозрастных коэффициентов и географических коэффициентов для системы здравоохранения Кыргызстана.

## Основные функциональные блоки

### 1. Работа с данными о населении и посещениях
- Извлечение и обработка данных о численности населения
- Расчет половозрастных коэффициентов
- Анализ и расчет рабочей нагрузки медицинских организаций

### 2. Географические данные
- Обработка географических данных (высота, плотность населения, сельский коэффициент)
- Расчет географических коэффициентов для медицинских организаций

### 3. Бюджетные расчеты
- Репликация старых бюджетов для сравнения
- Симуляция новых бюджетов с различными коэффициентами
- Ребалансировка бюджетов для обеспечения справедливого распределения

### 4. Визуализация
- Создание графиков для сравнения бюджетных изменений
- Визуализация влияния различных коэффициентов на бюджеты организаций

## Технический стек
- ASP.NET Core 6 WebAPI
- Библиотеки:
    - CsvHelper для работы с CSV-файлами
    - EPPlus для работы с Excel-файлами
    - MathNet.Numerics для математических расчетов
    - ScottPlot для создания графиков

## Структура проекта

### Модели данных
- `HealthcareData.cs` - Данные о здравоохранении и медицинских организациях
- `AgeSexCoefficients.cs` - Половозрастные коэффициенты
- `WorkloadData.cs` - Данные о рабочей нагрузке
- `GeoData.cs` - Географические данные
- `MergedHcoData.cs` - Объединенные данные о медицинских организациях
- `BudgetReplicationData.cs` - Данные для репликации бюджетов
- `SimulationData.cs` - Данные для симуляции бюджетов

### Сервисы
- `DataService.cs` - Обработка данных о населении и посещениях
- `AgeSexCoefficientService.cs` - Расчет половозрастных коэффициентов
- `WorkloadCoefficientService.cs` - Расчет коэффициентов рабочей нагрузки
- `WorkloadGraphService.cs` - Создание графиков рабочей нагрузки
- `HcoMergeService.cs` - Объединение данных о медицинских организациях
- `BudgetReplicationService.cs` - Репликация старых бюджетов
- `GeoService.cs` - Обработка географических данных
- `BudgetSimulationService.cs` - Симуляция новых бюджетов
- `BudgetRebalancingService.cs` - Ребалансировка бюджетов

### Контроллеры
- `DataController.cs` - API для работы с данными
- `AgeSexController.cs` - API для работы с половозрастными коэффициентами
- `WorkloadController.cs` - API для работы с данными о рабочей нагрузке
- `HcoController.cs` - API для работы с данными о медицинских организациях
- `BudgetController.cs` - API для работы с базовыми бюджетными данными
- `GeoController.cs` - API для работы с географическими данными
- `BudgetSimulationController.cs` - API для симуляции бюджетов
- `BudgetRebalancingController.cs` - API для ребалансировки бюджетов

ПОРЯДОК ЗАПУСКА:
1) `/DataProcessing/process-population-visit-data`
2) `/api/Coefficients/age-sex-coefficients`
3) `/api/Coefficients/workload-coefficients`
4) `/api/Coefficients/workload-graphs`
5) `/api/Hco/merge-data`
6) `/api/budget/replicate-old-budgets`
7) `/api/geo/process-geo-data`
8) `/api/budget-simulation/simulate-budgets`
9) `/api/budget-rebalancing/rebalance`

1) `/api/healthcare-organization/process-organizations`
2) `/api/healthcare-data-enrichment/enrich-data`
3) `/api/distance-calculation/calculate-distances`
4) `/api/geo-scenario/create-scenarios`
5) `/api/census-data/process-census-data`
6) `/api/health-facility-data/process-health-facility-data`

## Запуск проекта

### Необходимые предварительные условия
- .NET Core 6 SDK
- Доступ к файловой системе для создания директорий (working, output, original)

### Шаги для запуска
1. Клонировать репозиторий
2. Выполнить `dotnet restore` для восстановления пакетов
3. Выполнить `dotnet build` для сборки проекта
4. Выполнить `dotnet run` для запуска API
5. Swagger UI будет доступен по адресу: https://localhost:5001/swagger