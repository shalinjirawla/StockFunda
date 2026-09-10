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

        [JsonPropertyName("net_profit")]
        public decimal? NetProfit { get; set; }

        [JsonPropertyName("eps")]
        public decimal? Eps { get; set; }

        [JsonPropertyName("net_profit_attributable_to_minority_interest")]
        public decimal? NetProfitAttributableToMinorityInterest { get; set; }

        [JsonPropertyName("other_equity")]
        public decimal? OtherEquity { get; set; }

        [JsonPropertyName("cash_flow_operating")]
        public decimal? CashFlowOperating { get; set; }

        [JsonPropertyName("capex")]
        public decimal? Capex { get; set; }

        [JsonPropertyName("net_cash_flow")]
        public decimal? NetCashFlow { get; set; }

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
}
