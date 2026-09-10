using AutoMapper;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockLens_BusinessLayer.Interfaces;
using StockLens_BusinessLayer.MapperProfile;
using StockLens_BusinessLayer.Services;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.DataContext;
using StockLens_Infrastructure.ExternalServices.BharatStock;
using StockLens_Infrastructure.ExternalServices.IndianApi;
using StockLens_Infrastructure.Repositories;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace StockLens_UnitTests
{
    public class LiveIndianApiIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public LiveIndianApiIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task LiveTest_SyncAndPersistAllFourIndianStocks()
        {
            var secretPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "UserSecrets", "stocklens-api-e6a839f1-469b-4682-841f-823908c69134", "secrets.json");

            if (!File.Exists(secretPath))
            {
                _output.WriteLine("User secrets not found. Skipping live test.");
                return;
            }

            var secretJson = await File.ReadAllTextAsync(secretPath);
            using var doc = JsonDocument.Parse(secretJson);
            var apiKey = doc.RootElement.GetProperty("IndianApi:ApiKey").GetString() ?? "";

            var services = new ServiceCollection();
            services.AddLogging(cfg => cfg.AddConsole().SetMinimumLevel(LogLevel.Information));

            // Use the real SQL Server database
            services.AddDbContext<StockLensDataContext>(options =>
            {
                options.UseSqlServer("Server=DESKTOP-J2CVHIQ;Database=StockLensDB;Trusted_Connection=True;Encrypt=False;");
            });

            services.Configure<IndianApiSettings>(opts =>
            {
                opts.BaseUrl = "https://stock.indianapi.in";
                opts.ApiKey = apiKey;
            });

            services.Configure<BharatStockSettings>(opts =>
            {
                opts.BaseUrl = "https://bharatstockapi.com";
                opts.ApiKey = "";
            });

            services.AddHttpClient<IIndianApiShareholdingClient, IndianApiShareholdingClient>(client =>
            {
                client.BaseAddress = new Uri("https://stock.indianapi.in/");
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            services.AddHttpClient<IShareholdingProvider, BharatStockShareholdingProvider>(client =>
            {
                client.BaseAddress = new Uri("https://bharatstockapi.com/");
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            services.AddScoped<IStockRepository, StockRepository>();
            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<IStockShareholdingRepository, StockShareholdingRepository>();
            services.AddScoped<IStockShareholdingService, StockShareholdingService>();
            services.AddAutoMapper(cfg => cfg.AddProfile<MapperProfile>());

            var sp = services.BuildServiceProvider();
            var service = sp.GetRequiredService<IStockShareholdingService>();
            var db = sp.GetRequiredService<StockLensDataContext>();

            string[] testStocks = new[] { "RELIANCE", "TCS", "HDFCBANK", "INFY" };

            foreach (var symbol in testStocks)
            {
                _output.WriteLine($"\n========================================================");
                _output.WriteLine($"TESTING LIVE SYNC FOR STOCK: {symbol}");
                _output.WriteLine($"========================================================");

                var result = await service.GetShareholdingBySymbolAsync(symbol, "NSE", forceRefresh: true);

                result.Should().NotBeNull();
                result.Symbol.Should().Be(symbol);
                result.Source.Should().Be("IndianAPI");
                result.History.Should().NotBeEmpty();
                result.History.Count.Should().BeGreaterThanOrEqualTo(4);

                _output.WriteLine($"Stock: {result.Symbol} ({result.CompanyName})");
                _output.WriteLine($"Source: {result.Source}");
                _output.WriteLine($"Current Period: {result.CurrentPeriod.Period} (Key: {result.CurrentPeriod.PeriodKey})");
                _output.WriteLine($"Promoter: {(result.CurrentPeriod.Promoter.HasValue ? result.CurrentPeriod.Promoter + "%" : "— (NULL)")}");
                _output.WriteLine($"FII/FPI: {(result.CurrentPeriod.Fii.HasValue ? result.CurrentPeriod.Fii + "%" : "— (NULL)")}");
                _output.WriteLine($"DII: {(result.CurrentPeriod.Dii.HasValue ? result.CurrentPeriod.Dii + "%" : "— (NULL)")}");
                _output.WriteLine($"Government: {(result.CurrentPeriod.Government.HasValue ? result.CurrentPeriod.Government + "%" : "— (NULL)")}");
                _output.WriteLine($"Public: {(result.CurrentPeriod.Public.HasValue ? result.CurrentPeriod.Public + "%" : "— (NULL)")}");
                _output.WriteLine($"Others: {(result.CurrentPeriod.Others.HasValue ? result.CurrentPeriod.Others + "%" : "— (NULL)")}");
                _output.WriteLine($"Shareholders: {(result.CurrentPeriod.ShareholdersCount.HasValue ? result.CurrentPeriod.ShareholdersCount.Value.ToString("N0") : "—")}");
                _output.WriteLine($"Total Reconciled: {result.CurrentPeriod.Total}% (Valid: {result.Validation?.IsValid})");
                _output.WriteLine($"QoQ Promoter Change: {result.Change.Promoter} pp ({result.RelativeChange.Promoter}% relative)");
                _output.WriteLine($"Total Historical Quarters in Response: {result.History.Count}");

                _output.WriteLine("\nHistorical Quarters Table:");
                _output.WriteLine("Period      | Date        | Promoter | FII/FPI  | DII      | Govt     | Public   | Others   | Total    | Shareholders");
                _output.WriteLine("------------------------------------------------------------------------------------------------------------------");
                foreach (var h in result.History)
                {
                    var p = h.Promoter.HasValue ? h.Promoter.Value.ToString("F2") + "%" : "—";
                    var f = h.Fii.HasValue ? h.Fii.Value.ToString("F2") + "%" : "—";
                    var d = h.Dii.HasValue ? h.Dii.Value.ToString("F2") + "%" : "—";
                    var g = h.Government.HasValue ? h.Government.Value.ToString("F2") + "%" : "—";
                    var pub = h.Public.HasValue ? h.Public.Value.ToString("F2") + "%" : "—";
                    var oth = h.Others.HasValue ? h.Others.Value.ToString("F2") + "%" : "—";
                    var tot = h.Total.HasValue ? h.Total.Value.ToString("F2") + "%" : "—";
                    var sh = h.ShareholdersCount.HasValue ? h.ShareholdersCount.Value.ToString("N0") : "—";
                    _output.WriteLine($"{h.Period,-11} | {h.PeriodKey,-11} | {p,8} | {f,8} | {d,8} | {g,8} | {pub,8} | {oth,8} | {tot,8} | {sh,12}");
                }

                // Verify specific rules
                if (symbol == "HDFCBANK")
                {
                    result.CurrentPeriod.Promoter.Should().Match(p => p == null || p == 0.00m, "HDFCBANK has no promoters and should remain null or 0.00%");
                }
                else
                {
                    result.CurrentPeriod.Promoter.Should().NotBeNull();
                }

                result.CurrentPeriod.Fii.Should().NotBeNull("FII holding should be populated");
                result.CurrentPeriod.Dii.Should().NotBeNull("DII holding should be populated");
                result.CurrentPeriod.Public.Should().NotBeNull("Public holding should be populated");
            }
        }
    }
}
