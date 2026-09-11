using Microsoft.EntityFrameworkCore;
using StockLens_BusinessLayer.Interfaces;
using StockLens_BusinessLayer.Services;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.DataContext;
using StockLens_Infrastructure.ExternalServices.BharatStock;
using StockLens_Infrastructure.ExternalServices.IndianApi;
using StockLens_BusinessLayer.MapperProfile;
using StockLens_Infrastructure.Repositories;
using StockLens_Infrastructure.Seeders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddNewtonsoftJson(options => options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Database Context
builder.Services.AddDbContext<StockLensDataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("StockLensConnString"));
});

// Configure IndianAPI Settings
builder.Services.Configure<IndianApiSettings>(builder.Configuration.GetSection(IndianApiSettings.SectionName));

// Configure IndianAPI HTTP Clients
builder.Services.AddHttpClient<IIndianApiNewsClient, IndianApiNewsClient>((serviceProvider, client) =>
{
    var config = builder.Configuration.GetSection(IndianApiSettings.SectionName).Get<IndianApiSettings>() ?? new IndianApiSettings();
    client.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 15);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpClient<IIndianApiShareholdingClient, IndianApiShareholdingClient>((serviceProvider, client) =>
{
    var config = builder.Configuration.GetSection(IndianApiSettings.SectionName).Get<IndianApiSettings>() ?? new IndianApiSettings();
    client.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 15);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Configure BharatStock Settings
builder.Services.Configure<BharatStockSettings>(builder.Configuration.GetSection(BharatStockSettings.SectionName));

// Configure BharatStock Provider (Strictly Mock in Development ONLY when UseMockData is true; Live Provider for Production)
var bharatConfig = builder.Configuration.GetSection(BharatStockSettings.SectionName).Get<BharatStockSettings>() ?? new BharatStockSettings();
if (builder.Environment.IsDevelopment() && bharatConfig.UseMockData)
{
    builder.Services.AddScoped<IShareholdingProvider, MockShareholdingProvider>();
    builder.Services.AddScoped<IFinancialProvider, MockFinancialProvider>();
}
else
{
    builder.Services.AddHttpClient<IShareholdingProvider, BharatStockShareholdingProvider>((serviceProvider, client) =>
    {
        client.BaseAddress = new Uri(bharatConfig.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(bharatConfig.TimeoutSeconds > 0 ? bharatConfig.TimeoutSeconds : 15);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    builder.Services.AddHttpClient<IFinancialProvider, BharatStockFinancialProvider>((serviceProvider, client) =>
    {
        client.BaseAddress = new Uri(bharatConfig.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(bharatConfig.TimeoutSeconds > 0 ? bharatConfig.TimeoutSeconds : 15);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });
}

// Register Yahoo Finance HTTP Client
builder.Services.AddHttpClient<StockLens_Infrastructure.ExternalServices.YahooFinanceApi.IYahooFinanceClient, StockLens_Infrastructure.ExternalServices.YahooFinanceApi.YahooFinanceClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Register Repositories
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IStockNewsRepository, StockNewsRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<IStockShareholdingRepository, StockShareholdingRepository>();
builder.Services.AddScoped<IStockFinancialRepository, StockFinancialRepository>();
builder.Services.AddScoped<IStockBalanceSheetRepository, StockBalanceSheetRepository>();

// Register Seeders
builder.Services.AddTransient<CompanyMasterSeeder>();
builder.Services.AddHttpClient<IIndianApiNewsClient, IndianApiNewsClient>();
builder.Services.AddHttpClient<IIndianApiFinancialsClient, IndianApiFinancialsClient>();

// Register Business Services
builder.Services.AddScoped<IStockNewsService, StockNewsService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IStockShareholdingService, StockShareholdingService>();
builder.Services.AddScoped<IStockCashflowService, StockCashflowService>();
builder.Services.AddScoped<IStockBalanceSheetService, StockBalanceSheetService>();

// Register AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MapperProfile>());

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Execute Seeders on Startup
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<CompanyMasterSeeder>();
    await seeder.SeedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
