using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace StockLens_Infrastructure.ExternalServices.BharatStock.Models
{
    public class BharatStockFinancialRecord
    {
        [JsonPropertyName("period_type")]
        public string? PeriodType { get; set; } = "annual";

        [JsonPropertyName("fiscal_year")]
        public string? FiscalYear { get; set; }

        [JsonPropertyName("period_end_date")]
        public string? PeriodEndDateString { get; set; }

        [JsonPropertyName("revenue")]
        public decimal? Revenue { get; set; }

        [JsonPropertyName("revenue_from_operations")]
        public decimal? RevenueFromOperations { get; set; }

        [JsonPropertyName("operating_revenue")]
        public decimal? OperatingRevenue { get; set; }

        [JsonIgnore]
        public decimal? ResolvedRevenueFromOperations => RevenueFromOperations ?? OperatingRevenue ?? Revenue;

        [JsonPropertyName("operating_profit")]
        public decimal? OperatingProfit { get; set; }

        [JsonPropertyName("ebitda")]
        public decimal? Ebitda { get; set; }

        [JsonPropertyName("operating_profit_ebitda")]
        public decimal? OperatingProfitEbitda { get; set; }

        [JsonIgnore]
        public decimal? ResolvedOperatingProfitEbitda => Ebitda ?? OperatingProfitEbitda ?? OperatingProfit;

        [JsonPropertyName("net_profit")]
        public decimal? NetProfit { get; set; }

        [JsonPropertyName("eps")]
        public decimal? Eps { get; set; }

        [JsonPropertyName("net_profit_attributable_to_minority_interest")]
        public decimal? NetProfitAttributableToMinorityInterest { get; set; }

        [JsonPropertyName("other_equity")]
        public decimal? OtherEquity { get; set; }

        [JsonPropertyName("total_equity")]
        public decimal? TotalEquity { get; set; }

        [JsonPropertyName("shareholders_equity")]
        public decimal? ShareholdersEquity { get; set; }

        [JsonPropertyName("equity_share_capital")]
        public decimal? EquityShareCapital { get; set; }

        [JsonPropertyName("equity_capital")]
        public decimal? EquityCapital { get; set; }

        [JsonPropertyName("reserves")]
        public decimal? Reserves { get; set; }

        [JsonPropertyName("total_assets")]
        public decimal? TotalAssets { get; set; }

        [JsonPropertyName("total_liabilities")]
        public decimal? TotalLiabilities { get; set; }

        [JsonPropertyName("cash_flow_operating")]
        public decimal? CashFlowOperating { get; set; }

        [JsonPropertyName("capex")]
        public decimal? Capex { get; set; }

        [JsonPropertyName("net_cash_flow")]
        public decimal? NetCashFlow { get; set; }

        [JsonPropertyName("net_change_in_cash")]
        public decimal? NetChangeInCash { get; set; }

        [JsonIgnore]
        public decimal? ResolvedNetCashFlow => NetCashFlow ?? NetChangeInCash;

        [JsonPropertyName("consolidation_type")]
        public string? ConsolidationType { get; set; } = "consolidated";

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonIgnore]
        public DateTime? ResolvedPeriodEndDate
        {
            get
            {
                if (string.IsNullOrWhiteSpace(PeriodEndDateString)) return null;

                if (DateTime.TryParseExact(PeriodEndDateString.Trim(),
                    new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var parsed))
                {
                    return parsed;
                }

                if (DateTime.TryParse(PeriodEndDateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallbackParsed))
                {
                    return fallbackParsed;
                }

                return null;
            }
        }

        [JsonIgnore]
        public string ResolvedFiscalYear
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(FiscalYear))
                {
                    var clean = FiscalYear.Trim().ToUpperInvariant();
                    return clean.StartsWith("FY") ? clean : $"FY{clean}";
                }

                if (ResolvedPeriodEndDate.HasValue)
                {
                    var date = ResolvedPeriodEndDate.Value;
                    // Indian FY ends in March (e.g. 2025-03-31 -> FY25)
                    var year = date.Month <= 3 ? date.Year : date.Year + 1;
                    return $"FY{year % 100:D2}";
                }

                return "FY--";
            }
        }

        [JsonIgnore]
        public string ResolvedPeriodKey
        {
            get
            {
                var type = !string.IsNullOrWhiteSpace(PeriodType) ? PeriodType.Trim().ToLowerInvariant() : "annual";
                return $"{type}-{ResolvedFiscalYear}";
            }
        }
    }

    public class BharatStockFinancialApiResponseWrapper
    {
        [JsonPropertyName("data")]
        public List<BharatStockFinancialRecord>? Data { get; set; }

        [JsonPropertyName("financials")]
        public List<BharatStockFinancialRecord>? Financials { get; set; }

        [JsonPropertyName("results")]
        public List<BharatStockFinancialRecord>? Results { get; set; }
    }

    public class BharatStockRatiosRecord
    {
        [JsonPropertyName("as_of_date")]
        public string? AsOfDate { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("pe_ratio")]
        public decimal? PeRatio { get; set; }

        [JsonPropertyName("pb_ratio")]
        public decimal? PbRatio { get; set; }

        [JsonPropertyName("roe")]
        public decimal? Roe { get; set; }

        [JsonPropertyName("roce")]
        public decimal? Roce { get; set; }

        [JsonPropertyName("dividend_yield")]
        public decimal? DividendYield { get; set; }

        [JsonPropertyName("week_52_high")]
        public decimal? Week52High { get; set; }

        [JsonPropertyName("week_52_low")]
        public decimal? Week52Low { get; set; }

        [JsonPropertyName("financials_period_type")]
        public string? FinancialsPeriodType { get; set; }

        [JsonPropertyName("financials_fiscal_year")]
        public string? FinancialsFiscalYear { get; set; }
    }

    public class BharatStockCompanyDetailsRecord
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("company_name")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("sector")]
        public string? Sector { get; set; }

        [JsonPropertyName("exchange")]
        public string? Exchange { get; set; }

        [JsonPropertyName("industry")]
        public string? Industry { get; set; }

        [JsonPropertyName("face_value")]
        public decimal? FaceValue { get; set; }

        [JsonPropertyName("latest_price")]
        public BharatStockLatestPriceRecord? LatestPrice { get; set; }
    }

    public class BharatStockLatestPriceRecord
    {
        [JsonPropertyName("trade_date")]
        public string? TradeDate { get; set; }

        [JsonPropertyName("close")]
        public decimal? Close { get; set; }

        [JsonPropertyName("prev_close")]
        public decimal? PrevClose { get; set; }

        [JsonPropertyName("volume")]
        public long? Volume { get; set; }

        [JsonPropertyName("delivery_pct")]
        public decimal? DeliveryPct { get; set; }
    }

    public class BharatStockScreenerRecord
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("company_name")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("sector")]
        public string? Sector { get; set; }

        [JsonPropertyName("exchange")]
        public string? Exchange { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("market_cap")]
        public decimal? MarketCap { get; set; }

        [JsonPropertyName("pe_ratio")]
        public decimal? PeRatio { get; set; }

        [JsonPropertyName("pb_ratio")]
        public decimal? PbRatio { get; set; }

        [JsonPropertyName("roe")]
        public decimal? Roe { get; set; }

        [JsonPropertyName("roce")]
        public decimal? Roce { get; set; }

        [JsonPropertyName("book_value_per_share")]
        public decimal? BookValuePerShare { get; set; }

        [JsonPropertyName("book_value")]
        public decimal? BookValue { get; set; }

        [JsonIgnore]
        public decimal? ResolvedBookValue => BookValuePerShare ?? BookValue;

        [JsonPropertyName("net_profit_ttm")]
        public decimal? NetProfitTtm { get; set; }

        [JsonPropertyName("revenue_ttm")]
        public decimal? RevenueTtm { get; set; }

        [JsonPropertyName("eps")]
        public decimal? Eps { get; set; }

        [JsonPropertyName("free_cash_flow")]
        public decimal? FreeCashFlow { get; set; }

        [JsonPropertyName("computed_at")]
        public string? ComputedAt { get; set; }
    }

    public class BharatStockScreenerApiResponseWrapper
    {
        [JsonPropertyName("data")]
        public List<BharatStockScreenerRecord>? Data { get; set; }

        [JsonPropertyName("results")]
        public List<BharatStockScreenerRecord>? Results { get; set; }

        [JsonPropertyName("items")]
        public List<BharatStockScreenerRecord>? Items { get; set; }
    }
}


