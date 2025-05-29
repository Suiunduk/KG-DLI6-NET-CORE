using KG_DLI6_NET_CORE.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register our data service
builder.Services.AddScoped<DataService>();
builder.Services.AddScoped<AgeSexCoefficientService>();
builder.Services.AddScoped<WorkloadCoefficientService>();
builder.Services.AddScoped<WorkloadGraphService>();
builder.Services.AddScoped<HcoMergeService>();
builder.Services.AddScoped<BudgetReplicationService>();
builder.Services.AddScoped<GeoService>();
builder.Services.AddScoped<BudgetSimulationService>();
builder.Services.AddScoped<BudgetRebalancingService>();
builder.Services.AddScoped<HealthcareOrganizationService>();
builder.Services.AddScoped<HealthcareDataEnrichmentService>();
builder.Services.AddScoped<DistanceCalculationService>();
builder.Services.AddScoped<GeoScenarioService>();
builder.Services.AddScoped<CensusDataService>();
builder.Services.AddScoped<HealthFacilityDataService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();


// Создание директорий для работы приложения
var originalPath = Path.Combine(Directory.GetCurrentDirectory(), "original");
var workingPath = Path.Combine(Directory.GetCurrentDirectory(), "working");
var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "output");

Directory.CreateDirectory(originalPath);
Directory.CreateDirectory(workingPath);
Directory.CreateDirectory(outputPath);


// Настройка доступа к статическим файлам (графикам) в выходной директории
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(outputPath),
    RequestPath = "/output"
});

AppContext.SetSwitch("System.Drawing.EnableUnixSupport", true);

app.Run();