using Microsoft.EntityFrameworkCore;
using StockLens_BusinessLayer.Interfaces;
using StockLens_BusinessLayer.MapperProfile;
using StockLens_BusinessLayer.Services;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.DataContext;
using StockLens_Infrastructure.ExternalServices.IndianApi;
using StockLens_Infrastructure.Repositories;

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

// Configure IndianAPI HTTP Client
builder.Services.AddHttpClient<IIndianApiNewsClient, IndianApiNewsClient>((serviceProvider, client) =>
{
    var config = builder.Configuration.GetSection(IndianApiSettings.SectionName).Get<IndianApiSettings>() ?? new IndianApiSettings();
    client.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 15);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Register Repositories
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IStockNewsRepository, StockNewsRepository>();

// Register Business Services
builder.Services.AddScoped<IStockNewsService, StockNewsService>();

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

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
