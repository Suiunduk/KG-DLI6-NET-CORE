// Services/DataService.cs
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using DocumentFormat.OpenXml.Spreadsheet;
using KG_DLI6_NET_CORE.Models;

namespace KG_DLI6_NET_CORE.Services
{
    public class DataService
    {
        private readonly ILogger<DataService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _originalPath;
        private readonly string _workingPath;
        private readonly string _outputPath;

        public DataService(ILogger<DataService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            _originalPath = Path.Combine(Directory.GetCurrentDirectory(), "original");
            _workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
            _outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
            
            // Create directories if they don't exist
            Directory.CreateDirectory(_originalPath);
            Directory.CreateDirectory(_workingPath);
            Directory.CreateDirectory(_outputPath);
        }
        
        public async Task<List<PopulationVisitData>> ProcessPopulationVisitDataAsync()
        {
            _logger.LogInformation("Starting processing population and visit data");
            
            // 1. Read old data from for_foms.csv
            var forFomsPath = Path.Combine(_originalPath, "for_foms.csv");
            _logger.LogInformation($"Extracting data from: {forFomsPath}");
            
            // Detect encoding
            Encoding encoding = DetectFileEncoding(forFomsPath);
            _logger.LogInformation($"Detected encoding: {encoding.WebName}");
            
            List<PopulationVisitData> oldData = new List<PopulationVisitData>();
            
            // Read CSV with detected encoding using pipe delimiter
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = "|",
                HasHeaderRecord = true,
                BadDataFound = null
            };
            
            using (var reader = new StreamReader(forFomsPath, encoding))
            using (var csv = new CsvReader(reader, config))
            {
                // Define column mappings to match Python pandas rename operation
                csv.Context.RegisterClassMap<PopulationVisitDataMap>();
                oldData = csv.GetRecords<PopulationVisitData>().ToList();
            }
            
            // 2. Read new data from "population & visits.csv"
            var popVisitsPath = Path.Combine(_originalPath, "population & visits.csv");
            _logger.LogInformation($"Extracting data from: {popVisitsPath}");
            
            // Detect encoding
            encoding = DetectFileEncoding(popVisitsPath);
            _logger.LogInformation($"Detected encoding: {encoding.WebName}");
            
            List<PopulationVisitData> newData = new List<PopulationVisitData>();
            
            // Read CSV with detected encoding using semicolon delimiter
            config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true
            };
            
            using (var reader = new StreamReader(popVisitsPath, encoding))
            using (var csv = new CsvReader(reader, config))
            {
                // Define column mappings to match Python pandas rename operation
                csv.Context.RegisterClassMap<PopulationVisitsNewDataMap>();
                newData = csv.GetRecords<PopulationVisitData>().ToList();
            }
            
            // Process the data similar to Python logic
            var data = ProcessData(newData);
            
            // Create pivot tables and save data
            await CreatePivotTablesAndSaveAsync(data);
            
            return data;
        }
        
        private Encoding DetectFileEncoding(string filePath)
        {
            // Simple encoding detection - you might want to use a library like Ude.NetStandard
            // for more accurate encoding detection
            using (var reader = new StreamReader(filePath, Encoding.UTF8, true))
            {
                reader.Peek(); // Force detection
                return reader.CurrentEncoding;
            }
        }
        
        private List<PopulationVisitData> ProcessData(List<PopulationVisitData> data)
        {
            // Fix entries for HCO that have been merged
            foreach (var item in data.Where(d => d.Scm_NewCode == 620391).ToList())
            {
                item.Scm_NewCode = 620371;
            }
            
            // Group by Scm_NewCode and Age, summing numeric values
            var groupedData = data
                .GroupBy(x => new { x.Scm_NewCode, x.Age })
                .Select(g => new PopulationVisitData
                {
                    Scm_NewCode = g.Key.Scm_NewCode,
                    Age = g.Key.Age,
                    Region = g.First().Region,
                    Raion = g.First().Raion,
                    SoateRegion = g.First().SoateRegion,
                    Soate_Raion = g.First().Soate_Raion,
                    Scm_Code = g.First().Scm_Code,
                    NewName = g.First().NewName,
                    FullName = g.First().FullName,
                    Total = g.Sum(x => x.Total),
                    Men = g.Sum(x => x.Men),
                    Women = g.Sum(x => x.Women),
                    VisitTotal = g.Sum(x => x.VisitTotal),
                    VisitMen = g.Sum(x => x.VisitMen),
                    VisitWomen = g.Sum(x => x.VisitWomen),
                    mhi = g.Sum(x => x.mhi)
                })
                .ToList();
            
            // Update specific record
            var specificRecord = groupedData.FirstOrDefault(x => x.Scm_NewCode == 620371);
            if (specificRecord != null)
            {
                specificRecord.Region = "Ошская область";
                specificRecord.Raion = "Ноокатский район";
                specificRecord.SoateRegion = 4170600000000000;
                specificRecord.Soate_Raion = 41706242000000000;
                specificRecord.Scm_Code = 6820;
                specificRecord.NewName = "ЦСМ НООКАТСКОГО РАЙОНА \"МЕДИГОС\"";
            }
            
            // Remove HCOs not funded by MHIF
            groupedData = groupedData
                .Where(x => x.Scm_NewCode != 102412)
                .Where(x => x.Scm_Code != 1322)
                .Where(x => x.Scm_NewCode != 0)
                .ToList();
            
            return groupedData;
        }
        
        private async Task CreatePivotTablesAndSaveAsync(List<PopulationVisitData> data)
        {
            // Create dictionaries to hold our pivot tables
            var mvisits = CreatePivotTable(data, "VisitMen");
            var fvisits = CreatePivotTable(data, "VisitWomen");
            var mpop = CreatePivotTable(data, "Men");
            var fpop = CreatePivotTable(data, "Women");
            
            // Create HCO list
            var hcoList = CreateHcoList(data);
            
            // Save the data
            await SaveDataAsync(mvisits, "mvisits");
            await SaveDataAsync(fvisits, "fvisits");
            await SaveDataAsync(mpop, "mpop");
            await SaveDataAsync(fpop, "fpop");
            await SaveHcoListAsync(hcoList);
            
            _logger.LogInformation("Data processing completed successfully");
        }
        
        private Dictionary<int, Dictionary<int, double>> CreatePivotTable(List<PopulationVisitData> data, string valueField)
        {
            var result = new Dictionary<int, Dictionary<int, double>>();
            
            // Group by Age
            for (int age = 0; age <= 99; age++)
            {
                var ageDict = new Dictionary<int, double>();
                
                // For each Scm_NewCode
                foreach (var code in data.Select(d => d.Scm_NewCode).Distinct())
                {
                    var value = data
                        .Where(d => d.Age == age && d.Scm_NewCode == code)
                        .Sum(d => 
                        {
                            return valueField switch
                            {
                                "VisitMen" => d.VisitMen,
                                "VisitWomen" => d.VisitWomen,
                                "Men" => d.Men,
                                "Women" => d.Women,
                                _ => 0
                            };
                        });
                    
                    ageDict[code] = value;
                }
                
                result[age] = ageDict;
            }
            
            // Add row 99 with totals
            var ageGroup99Values = new Dictionary<int, double>();
            foreach (var code in data.Select(d => d.Scm_NewCode).Distinct())
            {
                double total = 0;
                for (int age = 0; age <= 99; age++)
                {
                    if (result.ContainsKey(age) && result[age].ContainsKey(code))
                    {
                        total += result[age][code];
                    }
                }
                ageGroup99Values[code] = total;
            }
            
            // Replace row 99 with totals
            result[99] = ageGroup99Values;
            
            return result;
        }
        
        private List<HcoData> CreateHcoList(List<PopulationVisitData> data)
        {
            // Calculate nr_mhi by summing mhi for each Scm_NewCode
            var mhiSums = data
                .GroupBy(d => d.Scm_NewCode)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.mhi));
            
            // Create the HCO list
            var hcoList = data
                .GroupBy(d => d.Scm_NewCode)
                .Select(g => new HcoData
                {
                    Scm_NewCode = g.Key,
                    Region = g.First().Region,
                    Raion = g.First().Raion,
                    SoateRegion = g.First().SoateRegion,
                    Soate_Raion = g.First().Soate_Raion,
                    Scm_Code = g.First().Scm_Code,
                    NewName = g.First().NewName,
                    FullName = g.First().FullName,
                    nr_mhi = mhiSums.ContainsKey(g.Key) ? mhiSums[g.Key] : 0
                })
                .ToList();
            
            return hcoList;
        }
        
        private async Task SaveDataAsync<T>(T data, string fileName)
        {
            var filePath = Path.Combine(_workingPath, fileName);
            
            // For this example, serialize to JSON
            // In a real application, you might want to use a more efficient format
            string json = System.Text.Json.JsonSerializer.Serialize(data);
            await File.WriteAllTextAsync(filePath, json);
        }
        
        private async Task SaveHcoListAsync(List<HcoData> hcoList)
        {
            // Save as JSON
            await SaveDataAsync(hcoList, "hco_list");
            
            // Save as Excel
            var excelFilePath = Path.Combine(_outputPath, "hco_list.xlsx");
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("HCO List");
                
                // Add headers
                worksheet.Cell(1, 1).Value = "Scm_NewCode";
                worksheet.Cell(1, 2).Value = "Region";
                worksheet.Cell(1, 3).Value = "SoateRegion";
                worksheet.Cell(1, 4).Value = "Raion";
                worksheet.Cell(1, 5).Value = "Soate_Raion";
                worksheet.Cell(1, 6).Value = "NewName";
                worksheet.Cell(1, 7).Value = "FullName";
                worksheet.Cell(1, 8).Value = "Scm_Code";
                worksheet.Cell(1, 9).Value = "nr_mhi";
                
                // Add data
                for (int i = 0; i < hcoList.Count; i++)
                {
                    var row = i + 2;
                    worksheet.Cell(row, 1).Value = hcoList[i].Scm_NewCode;
                    worksheet.Cell(row, 2).Value = hcoList[i].Region;
                    worksheet.Cell(row, 3).Value = hcoList[i].SoateRegion;
                    worksheet.Cell(row, 4).Value = hcoList[i].Raion;
                    worksheet.Cell(row, 5).Value = hcoList[i].Soate_Raion;
                    worksheet.Cell(row, 6).Value = hcoList[i].NewName;
                    worksheet.Cell(row, 7).Value = hcoList[i].FullName;
                    worksheet.Cell(row, 8).Value = hcoList[i].Scm_Code;
                    worksheet.Cell(row, 9).Value = hcoList[i].nr_mhi;
                }
                
                workbook.SaveAs(excelFilePath);
            }
        }
    }
    
    // CsvHelper mapping classes
    public class PopulationVisitDataMap : ClassMap<PopulationVisitData>
    {
        public PopulationVisitDataMap()
        {
            Map(m => m.Region).Name("Region");
            Map(m => m.SoateRegion).Name("SoateRegion");
            Map(m => m.Raion).Name("Raion");
            Map(m => m.Soate_Raion).Name("SoateRaion7");  // Renamed to Soate_Raion
            Map(m => m.Scm_Code).Name("Scm_Code");
            Map(m => m.Scm_NewCode).Name("Scm_NewCode");
            Map(m => m.NewName).Name("NewName");
            Map(m => m.Age).Name("Age");
            Map(m => m.Total).Name("Total");
            Map(m => m.Men).Name("men");  // Renamed to Men
            Map(m => m.Women).Name("women");  // Renamed to Women
            Map(m => m.VisitTotal).Name("visitTotal");  // Renamed to VisitTotal
            Map(m => m.VisitMen).Name("VisitMen");
            Map(m => m.VisitWomen).Name("VisitWomen");
            Map(m => m.mhi).Name("mhi");
        }
    }
    
    public class PopulationVisitsNewDataMap : ClassMap<PopulationVisitData>
    {
        public PopulationVisitsNewDataMap()
        {
            Map(m => m.Region).Name("Region");
            Map(m => m.SoateRegion).Name("Region COATE");  // Renamed to SoateRegion
            Map(m => m.Raion).Name("rayon");  // Renamed to Raion
            Map(m => m.Soate_Raion).Name("rayon COATE");  // Renamed to Soate_Raion
            Map(m => m.Scm_NewCode).Name("code of the health facility");  // Renamed to Scm_NewCode
            Map(m => m.NewName).Name("name of the heath facility");  // Renamed to NewName
            Map(m => m.FullName).Name("Full name of the organization");  // Renamed to FullName
            Map(m => m.Age).Name("age");  // Renamed to Age
            Map(m => m.Total).Name("total population");  // Renamed to Total
            Map(m => m.Men).Name("male (population)");  // Renamed to Men
            Map(m => m.Women).Name("female (population)");  // Renamed to Women
            Map(m => m.VisitTotal).Name("total visits");  // Renamed to VisitTotal
            Map(m => m.VisitMen).Name("male visits");  // Renamed to VisitMen
            Map(m => m.VisitWomen).Name("female visits");  // Renamed to VisitWomen
            Map(m => m.mhi).Name("insured total population");  // Renamed to mhi
        }
    }
}