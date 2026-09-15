using Microsoft.Extensions.Logging;
using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.BharatStock
{
    public class MockFinancialProvider : IFinancialProvider
    {
        private readonly ILogger<MockFinancialProvider> _logger;

        public MockFinancialProvider(ILogger<MockFinancialProvider> logger)
        {
            _logger = logger;
        }

        public Task<IReadOnlyList<BharatStockFinancialRecord>> GetFinancialsAsync(
            string ticker,
            string periodType = "annual",
            int page = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ticker))
            {
                throw new ArgumentException("Ticker is required.", nameof(ticker));
            }

            var clean = ticker.Trim().ToUpperInvariant();
            _logger.LogInformation("[DEV MOCK] Generating realistic annual cash flow & financial history for {Ticker}.", clean);

            var list = GenerateMockFinancials(clean);
            return Task.FromResult<IReadOnlyList<BharatStockFinancialRecord>>(list);
        }

        public Task<BharatStockRatiosRecord?> GetRatiosAsync(
            string ticker,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ticker))
            {
                throw new ArgumentException("Ticker is required.", nameof(ticker));
            }

            var clean = ticker.Trim().ToUpperInvariant();
            _logger.LogInformation("[DEV MOCK] Generating realistic ratios for {Ticker}.", clean);

            var ratios = GenerateMockRatios(clean);
            return Task.FromResult<BharatStockRatiosRecord?>(ratios);
        }

        public Task<BharatStockCompanyDetailsRecord?> GetStockDetailsAsync(
            string ticker,
            string? exchange = "NSE",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ticker))
            {
                throw new ArgumentException("Ticker is required.", nameof(ticker));
            }

            var clean = ticker.Trim().ToUpperInvariant();
            var cleanExchange = string.IsNullOrWhiteSpace(exchange) ? "NSE" : exchange.Trim().ToUpperInvariant();
            _logger.LogInformation("[DEV MOCK] Generating realistic company details & face value for {Ticker} ({Exchange}).", clean, cleanExchange);

            var details = GenerateMockStockDetails(clean, cleanExchange);
            return Task.FromResult<BharatStockCompanyDetailsRecord?>(details);
        }

        public Task<BharatStockScreenerRecord?> GetScreenerDataAsync(
            string ticker,
            string? exchange = "NSE",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ticker))
            {
                throw new ArgumentException("Ticker is required.", nameof(ticker));
            }

            var clean = ticker.Trim().ToUpperInvariant();
            var cleanExchange = string.IsNullOrWhiteSpace(exchange) ? "NSE" : exchange.Trim().ToUpperInvariant();
            _logger.LogInformation("[DEV MOCK] Generating realistic screener metrics (Book Value & Market Cap) for {Ticker} ({Exchange}).", clean, cleanExchange);

            var screener = GenerateMockScreenerData(clean, cleanExchange);
            return Task.FromResult<BharatStockScreenerRecord?>(screener);
        }

        public Task<IReadOnlyList<BharatStockScreenerRecord>> GetScreenerBySectorAsync(
            string sector,
            string? exchange = "NSE",
            int page = 1,
            int pageSize = 200,
            CancellationToken cancellationToken = default)
        {
            // Zero mock data: Return empty list so Sector P/E only uses genuine real API data or stays null
            return Task.FromResult<IReadOnlyList<BharatStockScreenerRecord>>(Array.Empty<BharatStockScreenerRecord>());
        }

        public static BharatStockCompanyDetailsRecord GenerateMockStockDetails(string ticker, string exchange = "NSE")
        {
            return ticker switch
            {
                "RELIANCE" => new BharatStockCompanyDetailsRecord
                {
                    Symbol = "RELIANCE",
                    CompanyName = "Reliance Industries Limited",
                    Sector = "Oil Gas & Consumable Fuels",
                    Exchange = exchange,
                    Industry = "Petroleum Products",
                    FaceValue = 10.0m,
                    LatestPrice = new BharatStockLatestPriceRecord
                    {
                        TradeDate = "2026-08-12",
                        Close = 1255.80m,
                        PrevClose = 1257.50m,
                        Volume = 8452110,
                        DeliveryPct = 42.6m
                    }
                },
                "TCS" => new BharatStockCompanyDetailsRecord
                {
                    Symbol = "TCS",
                    CompanyName = "Tata Consultancy Services Limited",
                    Sector = "Information Technology",
                    Exchange = exchange,
                    Industry = "IT Services & Consulting",
                    FaceValue = 1.0m,
                    LatestPrice = new BharatStockLatestPriceRecord
                    {
                        TradeDate = "2026-08-12",
                        Close = 4120.50m,
                        PrevClose = 4095.00m,
                        Volume = 2341200,
                        DeliveryPct = 58.4m
                    }
                },
                "INFY" => new BharatStockCompanyDetailsRecord
                {
                    Symbol = "INFY",
                    CompanyName = "Infosys Limited",
                    Sector = "Information Technology",
                    Exchange = exchange,
                    Industry = "IT Services & Consulting",
                    FaceValue = 5.0m,
                    LatestPrice = new BharatStockLatestPriceRecord
                    {
                        TradeDate = "2026-08-12",
                        Close = 1850.25m,
                        PrevClose = 1835.00m,
                        Volume = 4512000,
                        DeliveryPct = 61.2m
                    }
                },
                "TATAMOTORS" => new BharatStockCompanyDetailsRecord
                {
                    Symbol = "TATAMOTORS",
                    CompanyName = "Tata Motors Limited",
                    Sector = "Automobile",
                    Exchange = exchange,
                    Industry = "Commercial Vehicles",
                    FaceValue = 2.0m,
                    LatestPrice = new BharatStockLatestPriceRecord
                    {
                        TradeDate = "2026-08-12",
                        Close = 985.40m,
                        PrevClose = 970.00m,
                        Volume = 6200000,
                        DeliveryPct = 45.0m
                    }
                },
                "HDFCBANK" => new BharatStockCompanyDetailsRecord
                {
                    Symbol = "HDFCBANK",
                    CompanyName = "HDFC Bank Limited",
                    Sector = "Financial Services",
                    Exchange = exchange,
                    Industry = "Private Sector Bank",
                    FaceValue = 1.0m,
                    LatestPrice = new BharatStockLatestPriceRecord
                    {
                        TradeDate = "2026-08-12",
                        Close = 1680.00m,
                        PrevClose = 1665.00m,
                        Volume = 9100000,
                        DeliveryPct = 68.0m
                    }
                },
                "ICICIBANK" => new BharatStockCompanyDetailsRecord
                {
                    Symbol = "ICICIBANK",
                    CompanyName = "ICICI Bank Limited",
                    Sector = "Financial Services",
                    Exchange = exchange,
                    Industry = "Private Sector Bank",
                    FaceValue = 2.0m,
                    LatestPrice = new BharatStockLatestPriceRecord
                    {
                        TradeDate = "2026-08-12",
                        Close = 1240.00m,
                        PrevClose = 1225.00m,
                        Volume = 7800000,
                        DeliveryPct = 64.0m
                    }
                },
                _ => new BharatStockCompanyDetailsRecord
                {
                    Symbol = ticker,
                    CompanyName = $"{ticker} Limited",
                    Sector = "Diversified",
                    Exchange = exchange,
                    Industry = "Diversified Operations",
                    FaceValue = 10.0m,
                    LatestPrice = new BharatStockLatestPriceRecord
                    {
                        TradeDate = "2026-08-12",
                        Close = 980.00m,
                        PrevClose = 970.00m,
                        Volume = 1200000,
                        DeliveryPct = 50.0m
                    }
                }
            };
        }

        public static BharatStockScreenerRecord GenerateMockScreenerData(string ticker, string exchange = "NSE")
        {
            return ticker switch
            {
                "RELIANCE" => new BharatStockScreenerRecord
                {
                    Symbol = "RELIANCE",
                    CompanyName = "Reliance Industries Limited",
                    Sector = "Oil Gas & Consumable Fuels",
                    Exchange = exchange,
                    Price = 1255.80m,
                    MarketCap = 1701107.00m, // In Crores
                    PeRatio = 24.3m,
                    PbRatio = 2.1m,
                    BookValuePerShare = 1120.50m,
                    BookValue = 1120.50m,
                    Roe = 8.9m,
                    Roce = 10.4m,
                    NetProfitTtm = 79020.0m, // In Crores
                    RevenueTtm = 1000122.0m,
                    Eps = 58.60m,
                    FreeCashFlow = 69197000000.0m,
                    ComputedAt = "2026-08-15"
                },
                "TCS" => new BharatStockScreenerRecord
                {
                    Symbol = "TCS",
                    CompanyName = "Tata Consultancy Services Limited",
                    Sector = "Information Technology",
                    Exchange = exchange,
                    Price = 4120.50m,
                    MarketCap = 1492800.00m,
                    PeRatio = 28.5m,
                    PbRatio = 12.4m,
                    BookValuePerShare = 332.25m,
                    BookValue = 332.25m,
                    Roe = 48.2m,
                    Roce = 58.6m,
                    NetProfitTtm = 46580.0m,
                    RevenueTtm = 240893.0m,
                    Eps = 128.40m,
                    FreeCashFlow = 44720000000.0m,
                    ComputedAt = "2026-08-15"
                },
                "INFY" => new BharatStockScreenerRecord
                {
                    Symbol = "INFY",
                    CompanyName = "Infosys Limited",
                    Sector = "Information Technology",
                    Exchange = exchange,
                    Price = 1850.25m,
                    MarketCap = 768250.00m,
                    PeRatio = 26.1m,
                    PbRatio = 7.8m,
                    BookValuePerShare = 237.40m,
                    BookValue = 237.40m,
                    Roe = 31.5m,
                    Roce = 41.2m,
                    NetProfitTtm = 26248.0m,
                    RevenueTtm = 153670.0m,
                    Eps = 63.40m,
                    FreeCashFlow = 24750000000.0m,
                    ComputedAt = "2026-08-15"
                },
                "TATAMOTORS" => new BharatStockScreenerRecord
                {
                    Symbol = "TATAMOTORS",
                    CompanyName = "Tata Motors Limited",
                    Sector = "Automobile",
                    Exchange = exchange,
                    Price = 985.40m,
                    MarketCap = 345600.00m,
                    PeRatio = 16.8m,
                    PbRatio = 3.8m,
                    BookValuePerShare = 260.50m,
                    BookValue = 260.50m,
                    Roe = 22.4m,
                    Roce = 19.8m,
                    NetProfitTtm = 31800.0m,
                    RevenueTtm = 437928.0m,
                    Eps = 82.50m,
                    FreeCashFlow = 18500000000.0m,
                    ComputedAt = "2026-08-15"
                },
                "HDFCBANK" => new BharatStockScreenerRecord
                {
                    Symbol = "HDFCBANK",
                    CompanyName = "HDFC Bank Limited",
                    Sector = "Financial Services",
                    Exchange = exchange,
                    Price = 1680.00m,
                    MarketCap = 1250000.00m,
                    PeRatio = 18.5m,
                    PbRatio = 2.6m,
                    BookValuePerShare = 640.80m,
                    BookValue = 640.80m,
                    Roe = 16.8m,
                    Roce = 17.5m,
                    NetProfitTtm = 64000.0m,
                    RevenueTtm = 285000.0m,
                    Eps = 84.20m,
                    FreeCashFlow = 0m,
                    ComputedAt = "2026-08-15"
                },
                "ICICIBANK" => new BharatStockScreenerRecord
                {
                    Symbol = "ICICIBANK",
                    CompanyName = "ICICI Bank Limited",
                    Sector = "Financial Services",
                    Exchange = exchange,
                    Price = 1240.00m,
                    MarketCap = 860000.00m,
                    PeRatio = 17.2m,
                    PbRatio = 3.0m,
                    BookValuePerShare = 410.20m,
                    BookValue = 410.20m,
                    Roe = 18.2m,
                    Roce = 19.1m,
                    NetProfitTtm = 44000.0m,
                    RevenueTtm = 210000.0m,
                    Eps = 62.80m,
                    FreeCashFlow = 0m,
                    ComputedAt = "2026-08-15"
                },
                _ => new BharatStockScreenerRecord
                {
                    Symbol = ticker,
                    CompanyName = $"{ticker} Limited",
                    Sector = "Diversified",
                    Exchange = exchange,
                    Price = 980.00m,
                    MarketCap = 45000.00m,
                    PeRatio = 21.0m,
                    PbRatio = 3.2m,
                    BookValuePerShare = 450.00m,
                    BookValue = 450.00m,
                    Roe = 14.5m,
                    Roce = 18.2m,
                    NetProfitTtm = 2140.0m,
                    RevenueTtm = 32000.0m,
                    Eps = 46.50m,
                    FreeCashFlow = 5000000000.0m,
                    ComputedAt = "2026-08-15"
                }
            };
        }

        public static BharatStockRatiosRecord GenerateMockRatios(string ticker)
        {
            return ticker switch
            {
                "RELIANCE" => new BharatStockRatiosRecord
                {
                    AsOfDate = "2026-08-12",
                    Price = 1255.80m,
                    PeRatio = 21.5m,
                    PbRatio = 2.1m,
                    Roe = 8.9m,
                    Roce = 10.4m,
                    DividendYield = 0.35m,
                    Week52High = 1551.0m,
                    Week52Low = 1201.6m,
                    FinancialsPeriodType = "annual",
                    FinancialsFiscalYear = "FY25"
                },
                "TCS" => new BharatStockRatiosRecord
                {
                    AsOfDate = "2026-08-12",
                    Price = 4120.50m,
                    PeRatio = 28.5m,
                    PbRatio = 12.4m,
                    Roe = 48.2m,
                    Roce = 58.6m,
                    DividendYield = 1.45m,
                    Week52High = 4580.0m,
                    Week52Low = 3450.0m,
                    FinancialsPeriodType = "annual",
                    FinancialsFiscalYear = "FY25"
                },
                "INFY" => new BharatStockRatiosRecord
                {
                    AsOfDate = "2026-08-12",
                    Price = 1850.25m,
                    PeRatio = 26.1m,
                    PbRatio = 7.8m,
                    Roe = 31.5m,
                    Roce = 41.2m,
                    DividendYield = 2.10m,
                    Week52High = 1990.0m,
                    Week52Low = 1380.0m,
                    FinancialsPeriodType = "annual",
                    FinancialsFiscalYear = "FY25"
                },
                _ => new BharatStockRatiosRecord
                {
                    AsOfDate = "2026-08-12",
                    Price = 980.00m,
                    PeRatio = 21.0m,
                    PbRatio = 3.2m,
                    Roe = 14.5m,
                    Roce = 18.2m,
                    DividendYield = 0.85m,
                    Week52High = 1150.0m,
                    Week52Low = 780.0m,
                    FinancialsPeriodType = "annual",
                    FinancialsFiscalYear = "FY25"
                }
            };
        }

        public static List<BharatStockFinancialRecord> GenerateMockFinancials(string ticker)
        {
            return ticker switch
            {
                "RELIANCE" => new List<BharatStockFinancialRecord>
                {
                    new()
                    {
                        PeriodType = "annual",
                        FiscalYear = "FY26",
                        PeriodEndDateString = "2026-03-31",
                        Revenue = 1171706.0m,
                        OperatingProfit = 207911.0m,
                        NetProfit = 95754.0m,
                        Eps = 59.69m,
                        NetProfitAttributableToMinorityInterest = 7400.0m,
                        OtherEquity = 1120000.0m,
                        TotalEquity = 845000.0m,
                        TotalAssets = 1950000.0m,
                        TotalLiabilities = 1105000.0m,
                        CashFlowOperating = 192113.0m,
                        Capex = 122916.0m,
                        NetCashFlow = 39475.0m,
                        ConsolidationType = "consolidated",
                        Source = "BharatStock (Mock XBRL)"
                    },
                    new()
                    {
                        PeriodType = "annual",
                        FiscalYear = "FY25",
                        PeriodEndDateString = "2025-03-31",
                        Revenue = 1000122.0m,
                        OperatingProfit = 178650.0m,
                        NetProfit = 79020.0m,
                        Eps = 58.60m,
                        NetProfitAttributableToMinorityInterest = 6821.0m,
                        OtherEquity = 1057071.0m,
                        TotalEquity = 794300.0m,
                        TotalAssets = 1812800.0m,
                        TotalLiabilities = 1018500.0m,
                        CashFlowOperating = 176980.0m,
                        Capex = 121500.0m,
                        NetCashFlow = 28500.0m,
                        ConsolidationType = "consolidated",
                        Source = "BharatStock (Mock XBRL)"
                    },
                    new()
                    {
                        PeriodType = "annual",
                        FiscalYear = "FY24",
                        PeriodEndDateString = "2024-03-31",
                        Revenue = 891534.0m,
                        OperatingProfit = 160000.0m,
                        NetProfit = 69621.0m,
                        Eps = 51.40m,
                        NetProfitAttributableToMinorityInterest = 6010.0m,
                        OtherEquity = 974100.0m,
                        TotalEquity = 735200.0m,
                        TotalAssets = 1680000.0m,
                        TotalLiabilities = 944800.0m,
                        CashFlowOperating = 161820.0m,
                        Capex = 115200.0m,
                        NetCashFlow = 22100.0m,
                        ConsolidationType = "consolidated",
                        Source = "BharatStock (Mock XBRL)"
                    }
                },
                "TCS" => new List<BharatStockFinancialRecord>
                {
                    new()
                    {
                        PeriodType = "annual",
                        FiscalYear = "FY25",
                        PeriodEndDateString = "2025-03-31",
                        Revenue = 240893.0m,
                        OperatingProfit = 55000.0m,
                        NetProfit = 46580.0m,
                        Eps = 128.40m,
                        NetProfitAttributableToMinorityInterest = 420.0m,
                        OtherEquity = 98450.0m,
                        TotalEquity = 90127.0m,
                        TotalAssets = 142350.0m,
                        TotalLiabilities = 52223.0m,
                        CashFlowOperating = 48920.0m,
                        Capex = 4200.0m,
                        NetCashFlow = 3800.0m,
                        ConsolidationType = "consolidated",
                        Source = "BharatStock (Mock XBRL)"
                    },
                    new()
                    {
                        PeriodType = "annual",
                        FiscalYear = "FY24",
                        PeriodEndDateString = "2024-03-31",
                        Revenue = 225458.0m,
                        OperatingProfit = 51000.0m,
                        NetProfit = 43559.0m,
                        Eps = 119.20m,
                        NetProfitAttributableToMinorityInterest = 380.0m,
                        OtherEquity = 90210.0m,
                        TotalEquity = 82400.0m,
                        TotalAssets = 132000.0m,
                        TotalLiabilities = 49600.0m,
                        CashFlowOperating = 44340.0m,
                        Capex = 3850.0m,
                        NetCashFlow = 3200.0m,
                        ConsolidationType = "consolidated",
                        Source = "BharatStock (Mock XBRL)"
                    }
                },
                "INFY" => new List<BharatStockFinancialRecord>
                {
                    new()
                    {
                        PeriodType = "annual",
                        FiscalYear = "FY25",
                        PeriodEndDateString = "2025-03-31",
                        Revenue = 153670.0m,
                        OperatingProfit = 31000.0m,
                        NetProfit = 26248.0m,
                        Eps = 63.40m,
                        NetProfitAttributableToMinorityInterest = 150.0m,
                        OtherEquity = 74200.0m,
                        TotalEquity = 86450.0m,
                        TotalAssets = 125800.0m,
                        TotalLiabilities = 39350.0m,
                        CashFlowOperating = 27850.0m,
                        Capex = 3100.0m,
                        NetCashFlow = 2100.0m,
                        ConsolidationType = "consolidated",
                        Source = "BharatStock (Mock XBRL)"
                    },
                    new()
                    {
                        PeriodType = "annual",
                        FiscalYear = "FY24",
                        PeriodEndDateString = "2024-03-31",
                        Revenue = 146767.0m,
                        OperatingProfit = 28500.0m,
                        NetProfit = 24095.0m,
                        Eps = 58.10m,
                        NetProfitAttributableToMinorityInterest = 120.0m,
                        OtherEquity = 68900.0m,
                        TotalEquity = 78200.0m,
                        TotalAssets = 115000.0m,
                        TotalLiabilities = 36800.0m,
                        CashFlowOperating = 24980.0m,
                        Capex = 2850.0m,
                        NetCashFlow = 1900.0m,
                        ConsolidationType = "consolidated",
                        Source = "BharatStock (Mock XBRL)"
                    }
                },
                _ => new List<BharatStockFinancialRecord>
                {
                    new()
                    {
                        PeriodType = "annual",
                        FiscalYear = "FY25",
                        PeriodEndDateString = "2025-03-31",
                        Revenue = 120000.0m,
                        OperatingProfit = 20000.0m,
                        NetProfit = 15000.0m,
                        Eps = 42.50m,
                        NetProfitAttributableToMinorityInterest = 300.0m,
                        OtherEquity = 85000.0m,
                        TotalEquity = 52000.0m,
                        TotalAssets = 98000.0m,
                        TotalLiabilities = 46000.0m,
                        CashFlowOperating = 22000.0m,
                        Capex = 9500.0m,
                        NetCashFlow = 1800.0m,
                        ConsolidationType = "consolidated",
                        Source = "BharatStock (Mock XBRL)"
                    },
                    new()
                    {
                        PeriodType = "annual",
                        FiscalYear = "FY24",
                        PeriodEndDateString = "2024-03-31",
                        Revenue = 108000.0m,
                        OperatingProfit = 17500.0m,
                        NetProfit = 13200.0m,
                        Eps = 38.20m,
                        NetProfitAttributableToMinorityInterest = 250.0m,
                        OtherEquity = 76000.0m,
                        TotalEquity = 46500.0m,
                        TotalAssets = 89000.0m,
                        TotalLiabilities = 42500.0m,
                        CashFlowOperating = 19500.0m,
                        Capex = 8800.0m,
                        NetCashFlow = 1400.0m,
                        ConsolidationType = "consolidated",
                        Source = "BharatStock (Mock XBRL)"
                    }
                }
            };
        }
    }
}
