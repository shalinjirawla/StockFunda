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
        public async Task LiveTest_DirectIndianApiStockOverview()
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

            using var httpClient = new HttpClient { BaseAddress = new Uri("https://stock.indianapi.in/") };
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            var res = await httpClient.GetAsync("stock?name=RELIANCE");
            _output.WriteLine($"Status: {res.StatusCode}");
            var body = await res.Content.ReadAsStringAsync();
            _output.WriteLine($"Body length: {body.Length}");

            using var doc2 = JsonDocument.Parse(body);
            foreach (var prop in doc2.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    _output.WriteLine($"Key: {prop.Name} (Object) -> Subkeys: {string.Join(", ", prop.Value.EnumerateObject().Select(p => p.Name).Take(15))}");
                }
                else if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    _output.WriteLine($"Key: {prop.Name} (Array of {prop.Value.GetArrayLength()} items)");
                }
                else
                {
                    _output.WriteLine($"Key: {prop.Name} = {prop.Value}");
                }
            }

            using var httpClient2 = new HttpClient();
            var client = new IndianApiBalanceSheetClient(
                httpClient2,
                Microsoft.Extensions.Options.Options.Create(new IndianApiSettings { ApiKey = apiKey, BaseUrl = "https://stock.indianapi.in" }),
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<IndianApiBalanceSheetClient>());

            var result = await client.GetStockFinancialsAndOverviewAsync("RELIANCE");
            _output.WriteLine($"Result - CurrentPrice: {result?.CurrentPrice}");
            _output.WriteLine($"Result - YearHigh: {result?.YearHigh}");
            _output.WriteLine($"Result - YearLow: {result?.YearLow}");
            _output.WriteLine($"Result - FaceValue: {result?.FaceValue}");
            _output.WriteLine($"Result - MarketCap: {result?.MarketCap}");
            _output.WriteLine($"Result - PeRatio: {result?.PeRatio}");
            _output.WriteLine($"Result - Roe: {result?.Roe}");
            _output.WriteLine($"Result - Roce: {result?.Roce}");
            _output.WriteLine($"Result - BookValue: {result?.BookValue}");
            _output.WriteLine($"Result - Financials count: {result?.Financials?.Count}");

            result.Should().NotBeNull();
            result!.CurrentPrice.Should().BeGreaterThan(0);
            result.YearHigh.Should().BeGreaterThan(0);
            result.YearLow.Should().BeGreaterThan(0);
            result.FaceValue.Should().BeGreaterThan(0);
            result.MarketCap.Should().BeGreaterThan(0);
            result.PeRatio.Should().BeGreaterThan(0);
            result.Roe.Should().BeGreaterThan(0);
            result.Roce.Should().BeGreaterThan(0);
            result.BookValue.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task LiveTest_QuarterlyResultsDetailed()
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

            using var httpClient = new HttpClient { BaseAddress = new Uri("https://stock.indianapi.in/") };
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            var res = await httpClient.GetAsync("stock?name=RELIANCE");
            var body = await res.Content.ReadAsStringAsync();
            using var doc2 = JsonDocument.Parse(body);

            _output.WriteLine("\n--- ROOT PROPERTIES IN INDIANAPI PAYLOAD ---");
            foreach (var prop in doc2.RootElement.EnumerateObject())
            {
                _output.WriteLine($"[Root Key] {prop.Name} (Type: {prop.Value.ValueKind})");
                if (prop.Name.Equals("peerCompanyList", StringComparison.OrdinalIgnoreCase) || 
                    prop.Name.Equals("peers", StringComparison.OrdinalIgnoreCase) || 
                    prop.Name.Equals("peerList", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Equals("keyMetrics", StringComparison.OrdinalIgnoreCase) || 
                    prop.Name.Equals("companyProfile", StringComparison.OrdinalIgnoreCase))
                {
                    _output.WriteLine($"   Value: {prop.Value.ToString()}");
                }
            }
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
            services.AddLogging();

            services.AddDbContext<StockLensDataContext>(options =>
            {
                options.UseInMemoryDatabase("TestShareholdingLive_" + Guid.NewGuid());
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

        [Fact]
        public async Task LiveTest_StockCashflowServiceFaceValueAndMarketCap()
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
            services.AddLogging();
            services.AddMemoryCache();

            services.AddDbContext<StockLensDataContext>(options =>
            {
                options.UseInMemoryDatabase("TestCashflowLive_" + Guid.NewGuid());
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

            services.AddHttpClient<IIndianApiBalanceSheetClient, IndianApiBalanceSheetClient>((sp, client) =>
            {
                client.BaseAddress = new Uri("https://stock.indianapi.in/");
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            services.AddHttpClient<StockLens_Infrastructure.ExternalServices.YahooFinanceApi.IYahooFinanceClient, StockLens_Infrastructure.ExternalServices.YahooFinanceApi.YahooFinanceClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            services.AddScoped<IFinancialProvider, MockFinancialProvider>();
            services.AddScoped<IStockRepository, StockRepository>();
            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<IStockFinancialRepository, StockFinancialRepository>();
            services.AddScoped<IStockBalanceSheetRepository, StockBalanceSheetRepository>();
            services.AddScoped<ISectorValuationService, SectorValuationService>();
            services.AddScoped<IStockCashflowService, StockCashflowService>();
            services.AddAutoMapper(cfg => cfg.AddProfile<MapperProfile>());

            var sp = services.BuildServiceProvider();
            var service = sp.GetRequiredService<IStockCashflowService>();

            string[] testStocks = new[] { "RELIANCE", "TCS", "INFY", "HDFCBANK", "TATAMOTORS" };

            foreach (var sym in testStocks)
            {
                _output.WriteLine($"\n--- Testing {sym} (Cached / Normal Fetch) ---");
                var cached = await service.GetCashflowBySymbolAsync(sym, "NSE", forceRefresh: false);
                _output.WriteLine($"[{sym}] Cached FaceValue: {cached?.Ratios?.FaceValue}");
                _output.WriteLine($"[{sym}] Cached MarketCap: {cached?.Ratios?.MarketCap}");
                _output.WriteLine($"[{sym}] Cached CurrentPrice: {cached?.Ratios?.CurrentPrice}");
                _output.WriteLine($"[{sym}] Cached TotalEquity: {cached?.Ratios?.TotalEquity}");
                _output.WriteLine($"[{sym}] Cached BookValue: {cached?.Ratios?.BookValue}");
                _output.WriteLine($"[{sym}] Cached Roe: {cached?.Ratios?.Roe}");
                _output.WriteLine($"[{sym}] Cached Roce: {cached?.Ratios?.Roce}");
                _output.WriteLine($"[{sym}] Cached PeRatio: {cached?.Ratios?.PeRatio}");
                _output.WriteLine($"[{sym}] Cached PegRatio: {cached?.Ratios?.PegRatio}");
                _output.WriteLine($"[{sym}] Cached EpsGrowth: {cached?.Summary?.YoYChange?.EpsGrowth}");
                _output.WriteLine($"[{sym}] Cached NetProfitGrowth: {cached?.Summary?.YoYChange?.NetProfitGrowth}");
                _output.WriteLine($"[{sym}] Cached SectorPe: {cached?.Ratios?.SectorPe}");
                _output.WriteLine($"[{sym}] Cached SectorName: {cached?.Ratios?.SectorPeSector}");

                cached.Should().NotBeNull();
                cached!.Ratios.Should().NotBeNull();
                cached.Ratios!.FaceValue.Should().NotBeNull($"FaceValue should not be null for {sym}");
                cached.Ratios.MarketCap.Should().NotBeNull($"MarketCap should not be null for {sym}");
            }
        }

        [Fact]
        public async Task LiveTest_SyncQuarterlyResultsLiveEndToEnd()
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
            services.AddLogging();
            services.AddMemoryCache();

            services.AddDbContext<StockLensDataContext>(options =>
            {
                options.UseInMemoryDatabase("TestQuarterlyLive_" + Guid.NewGuid());
            });

            services.Configure<IndianApiSettings>(opts =>
            {
                opts.BaseUrl = "https://stock.indianapi.in";
                opts.ApiKey = apiKey;
            });

            services.AddHttpClient<IIndianApiBalanceSheetClient, IndianApiBalanceSheetClient>((sp, client) =>
            {
                client.BaseAddress = new Uri("https://stock.indianapi.in/");
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            services.AddHttpClient<StockLens_Infrastructure.ExternalServices.YahooFinanceApi.IYahooFinanceClient, StockLens_Infrastructure.ExternalServices.YahooFinanceApi.YahooFinanceClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            services.AddScoped<IStockRepository, StockRepository>();
            services.AddScoped<ICompanyRepository, CompanyRepository>();
            services.AddScoped<IStockFinancialRepository, StockFinancialRepository>();
            services.AddScoped<IStockQuarterlyResultsService, StockQuarterlyResultsService>();
            services.AddAutoMapper(cfg => cfg.AddProfile<MapperProfile>());

            var sp = services.BuildServiceProvider();
            var service = sp.GetRequiredService<IStockQuarterlyResultsService>();

            string[] testStocks = new[] { "RELIANCE", "TCS", "INFY", "HDFCBANK" };

            foreach (var sym in testStocks)
            {
                _output.WriteLine($"\n========================================================");
                _output.WriteLine($"TESTING LIVE QUARTERLY FOR STOCK: {sym}");
                _output.WriteLine($"========================================================");

                var res = await service.GetQuarterlyResultsBySymbolAsync(sym, "NSE", forceRefresh: true);

                res.Should().NotBeNull();
                _output.WriteLine($"Stock: {res.Symbol} | LatestQuarter: {res.LatestQuarter} | Source: {res.Source}");
                _output.WriteLine($"[Summary] Sales: {res.Summary.Sales} Cr | NetProfit: {res.Summary.NetProfit} Cr | EPS: {res.Summary.Eps} | Tax: {res.Summary.Tax} Cr | Depreciation: {res.Summary.Depreciation} Cr");

                _output.WriteLine("\n[History Quarters]");
                foreach (var h in res.History)
                {
                    _output.WriteLine($"  Period: {h.Period} | Date: {h.PeriodEndDate:yyyy-MM-dd} | Sales: {h.Sales} | NetProfit: {h.NetProfit} | EPS: {h.Eps} | Tax: {h.Tax} | Dep: {h.Depreciation}");
                }
            }
        }

        [Fact]
        public async Task LiveTest_InspectRelianceCfoAndOperatingProfit()
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

            using var httpClient = new HttpClient { BaseAddress = new Uri("https://stock.indianapi.in/") };
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            var res = await httpClient.GetAsync("stock?name=RELIANCE");
            var body = await res.Content.ReadAsStringAsync();
            using var doc2 = JsonDocument.Parse(body);

            _output.WriteLine("=== RAW FINANCIAL KEYS FOR RELIANCE ===");
            if (doc2.RootElement.TryGetProperty("financials", out var finObj) && finObj.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in finObj.EnumerateArray())
                {
                    if (item.TryGetProperty("FiscalYear", out var fyProp) && (fyProp.GetString() == "FY26" || fyProp.GetString() == "2026" || fyProp.GetString() == "FY25" || fyProp.GetString() == "2024" || fyProp.GetString() == "FY24"))
                    {
                        _output.WriteLine($"--- ALL RAW KEYS IN {fyProp.GetString()} ---");
                        if (item.TryGetProperty("stockFinancialMap", out var mapObj))
                        {
                            if (mapObj.TryGetProperty("INC", out var incArr) && incArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var incItem in incArr.EnumerateArray())
                                {
                                    if (incItem.TryGetProperty("key", out var k) && incItem.TryGetProperty("value", out var v))
                                    {
                                        _output.WriteLine($"  [INC] {k.GetString()} = {v}");
                                    }
                                }
                            }
                            if (mapObj.TryGetProperty("CAS", out var casArr) && casArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var casItem in casArr.EnumerateArray())
                                {
                                    if (casItem.TryGetProperty("key", out var k) && casItem.TryGetProperty("value", out var v))
                                    {
                                        _output.WriteLine($"  [CAS] {k.GetString()} = {v}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            using var httpClient2 = new HttpClient();
            var client = new IndianApiBalanceSheetClient(
                httpClient2,
                Microsoft.Extensions.Options.Options.Create(new IndianApiSettings { ApiKey = apiKey, BaseUrl = "https://stock.indianapi.in" }),
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<IndianApiBalanceSheetClient>());

            var result = await client.GetStockFinancialsAndOverviewAsync("RELIANCE");
            _output.WriteLine("\n=== PARSED FINANCIALS IN CLIENT ===");
            foreach (var f in result.Financials.Take(5))
            {
                _output.WriteLine($"FY: {f.FiscalYear} | PeriodEnd: {f.PeriodEndDate:yyyy-MM-dd} | Revenue: {f.Revenue} | OperatingProfit: {f.OperatingProfit} | NetProfit: {f.NetProfit} | CFO: {f.OperatingCashFlow} | Capex: {f.Capex} | FCF: {f.FreeCashFlow}");
                if (f.OperatingCashFlow.HasValue && f.OperatingProfit.HasValue && f.OperatingProfit.Value != 0)
                {
                    _output.WriteLine($"   -> CFO / OP: {f.OperatingCashFlow.Value} / {f.OperatingProfit.Value} = {f.OperatingCashFlow.Value / f.OperatingProfit.Value:F4} ({Math.Round((f.OperatingCashFlow.Value / f.OperatingProfit.Value) * 100, 2)}%)");
                }
                if (f.OperatingCashFlow.HasValue && f.NetProfit.HasValue && f.NetProfit.Value != 0)
                {
                    _output.WriteLine($"   -> CFO / NetProfit (PAT): {f.OperatingCashFlow.Value} / {f.NetProfit.Value} = {f.OperatingCashFlow.Value / f.NetProfit.Value:F4} ({Math.Round((f.OperatingCashFlow.Value / f.NetProfit.Value) * 100, 2)}%)");
                }
            }
        }
    }
}

