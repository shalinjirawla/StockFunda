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

        public static List<BharatStockFinancialRecord> GenerateMockFinancials(string ticker)
        {
            return ticker switch
            {
                "RELIANCE" => new List<BharatStockFinancialRecord>
                {
                    new() { PeriodType = "annual", FiscalYear = "FY25", PeriodEndDateString = "2025-03-31", Revenue = 964693.0m, NetProfit = 79020.0m, Eps = 58.60m, NetProfitAttributableToMinorityInterest = 6821.0m, OtherEquity = 1057071.0m, CashFlowOperating = 192113.0m, Capex = 128000.0m, NetCashFlow = 15200.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY24", PeriodEndDateString = "2024-03-31", Revenue = 891534.0m, NetProfit = 69621.0m, Eps = 51.40m, NetProfitAttributableToMinorityInterest = 6010.0m, OtherEquity = 974100.0m, CashFlowOperating = 176980.0m, Capex = 121500.0m, NetCashFlow = 11400.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY23", PeriodEndDateString = "2023-03-31", Revenue = 879468.0m, NetProfit = 66702.0m, Eps = 49.30m, NetProfitAttributableToMinorityInterest = 5540.0m, OtherEquity = 898230.0m, CashFlowOperating = 161820.0m, Capex = 115200.0m, NetCashFlow = 8900.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY22", PeriodEndDateString = "2022-03-31", Revenue = 721634.0m, NetProfit = 60705.0m, Eps = 44.80m, NetProfitAttributableToMinorityInterest = 4890.0m, OtherEquity = 812400.0m, CashFlowOperating = 145200.0m, Capex = 105400.0m, NetCashFlow = 6200.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY21", PeriodEndDateString = "2021-03-31", Revenue = 502980.0m, NetProfit = 49128.0m, Eps = 36.20m, NetProfitAttributableToMinorityInterest = 4210.0m, OtherEquity = 745300.0m, CashFlowOperating = 128400.0m, Capex = 94800.0m, NetCashFlow = 4100.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" }
                },
                "TCS" => new List<BharatStockFinancialRecord>
                {
                    new() { PeriodType = "annual", FiscalYear = "FY25", PeriodEndDateString = "2025-03-31", Revenue = 240893.0m, NetProfit = 46580.0m, Eps = 128.40m, NetProfitAttributableToMinorityInterest = 420.0m, OtherEquity = 98450.0m, CashFlowOperating = 48920.0m, Capex = 4200.0m, NetCashFlow = 3800.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY24", PeriodEndDateString = "2024-03-31", Revenue = 225458.0m, NetProfit = 43559.0m, Eps = 119.20m, NetProfitAttributableToMinorityInterest = 380.0m, OtherEquity = 90210.0m, CashFlowOperating = 44340.0m, Capex = 3850.0m, NetCashFlow = 3200.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY23", PeriodEndDateString = "2023-03-31", Revenue = 217411.0m, NetProfit = 42147.0m, Eps = 115.19m, NetProfitAttributableToMinorityInterest = 350.0m, OtherEquity = 84120.0m, CashFlowOperating = 42010.0m, Capex = 3600.0m, NetCashFlow = 2900.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY22", PeriodEndDateString = "2022-03-31", Revenue = 191754.0m, NetProfit = 38327.0m, Eps = 103.62m, NetProfitAttributableToMinorityInterest = 310.0m, OtherEquity = 78400.0m, CashFlowOperating = 39950.0m, Capex = 3200.0m, NetCashFlow = 2400.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" }
                },
                "INFY" => new List<BharatStockFinancialRecord>
                {
                    new() { PeriodType = "annual", FiscalYear = "FY25", PeriodEndDateString = "2025-03-31", Revenue = 153670.0m, NetProfit = 26248.0m, Eps = 63.40m, NetProfitAttributableToMinorityInterest = 150.0m, OtherEquity = 74200.0m, CashFlowOperating = 27850.0m, Capex = 3100.0m, NetCashFlow = 2100.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY24", PeriodEndDateString = "2024-03-31", Revenue = 146767.0m, NetProfit = 24095.0m, Eps = 58.10m, NetProfitAttributableToMinorityInterest = 120.0m, OtherEquity = 68900.0m, CashFlowOperating = 24980.0m, Capex = 2850.0m, NetCashFlow = 1900.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY23", PeriodEndDateString = "2023-03-31", Revenue = 142125.0m, NetProfit = 23970.0m, Eps = 57.60m, NetProfitAttributableToMinorityInterest = 110.0m, OtherEquity = 65400.0m, CashFlowOperating = 23800.0m, Capex = 2600.0m, NetCashFlow = 1750.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" }
                },
                _ => new List<BharatStockFinancialRecord>
                {
                    new() { PeriodType = "annual", FiscalYear = "FY25", PeriodEndDateString = "2025-03-31", Revenue = 120000.0m, NetProfit = 15000.0m, Eps = 42.50m, NetProfitAttributableToMinorityInterest = 300.0m, OtherEquity = 85000.0m, CashFlowOperating = 22000.0m, Capex = 9500.0m, NetCashFlow = 1800.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY24", PeriodEndDateString = "2024-03-31", Revenue = 108000.0m, NetProfit = 13200.0m, Eps = 38.20m, NetProfitAttributableToMinorityInterest = 250.0m, OtherEquity = 76000.0m, CashFlowOperating = 19500.0m, Capex = 8800.0m, NetCashFlow = 1400.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" },
                    new() { PeriodType = "annual", FiscalYear = "FY23", PeriodEndDateString = "2023-03-31", Revenue = 96000.0m, NetProfit = 11500.0m, Eps = 33.10m, NetProfitAttributableToMinorityInterest = 200.0m, OtherEquity = 68000.0m, CashFlowOperating = 17200.0m, Capex = 8000.0m, NetCashFlow = 1100.0m, ConsolidationType = "consolidated", Source = "BharatStock (Mock XBRL)" }
                }
            };
        }
    }
}
