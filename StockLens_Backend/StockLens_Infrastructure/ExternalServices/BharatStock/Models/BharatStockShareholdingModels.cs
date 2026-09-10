using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StockLens_Infrastructure.ExternalServices.BharatStock.Models
{
    public class BharatStockShareholdingRecord
    {
        private decimal? _promoterHolding;
        private decimal? _fiiHolding;
        private decimal? _diiHolding;
        private decimal? _publicHolding;

        // Promoter fields
        [JsonPropertyName("promoter_holding")]
        public decimal? PromoterHoldingRaw
        {
            get => _promoterHolding;
            set => _promoterHolding = value;
        }

        [JsonPropertyName("promoter_pct")]
        public decimal? PromoterPct
        {
            get => _promoterHolding;
            set => _promoterHolding = value;
        }

        [JsonPropertyName("promoter")]
        public decimal? Promoter
        {
            get => _promoterHolding;
            set => _promoterHolding = value;
        }

        [JsonIgnore]
        public decimal? PromoterHolding
        {
            get => _promoterHolding;
            set => _promoterHolding = value;
        }

        // FII / FPI fields
        [JsonPropertyName("fii_holding")]
        public decimal? FiiHoldingRaw
        {
            get => _fiiHolding;
            set => _fiiHolding = value;
        }

        [JsonPropertyName("fii_pct")]
        public decimal? FiiPct
        {
            get => _fiiHolding;
            set => _fiiHolding = value;
        }

        [JsonPropertyName("fpi_holding")]
        public decimal? FpiHolding
        {
            get => _fiiHolding;
            set => _fiiHolding = value;
        }

        [JsonPropertyName("fpi_pct")]
        public decimal? FpiPct
        {
            get => _fiiHolding;
            set => _fiiHolding = value;
        }

        [JsonPropertyName("fii")]
        public decimal? Fii
        {
            get => _fiiHolding;
            set => _fiiHolding = value;
        }

        [JsonIgnore]
        public decimal? FiiHolding
        {
            get => _fiiHolding;
            set => _fiiHolding = value;
        }

        // DII fields
        [JsonPropertyName("dii_holding")]
        public decimal? DiiHoldingRaw
        {
            get => _diiHolding;
            set => _diiHolding = value;
        }

        [JsonPropertyName("dii_pct")]
        public decimal? DiiPct
        {
            get => _diiHolding;
            set => _diiHolding = value;
        }

        [JsonPropertyName("dii")]
        public decimal? Dii
        {
            get => _diiHolding;
            set => _diiHolding = value;
        }

        [JsonIgnore]
        public decimal? DiiHolding
        {
            get => _diiHolding;
            set => _diiHolding = value;
        }

        // Public fields
        [JsonPropertyName("public_holding")]
        public decimal? PublicHoldingRaw
        {
            get => _publicHolding;
            set => _publicHolding = value;
        }

        [JsonPropertyName("public_pct")]
        public decimal? PublicPct
        {
            get => _publicHolding;
            set => _publicHolding = value;
        }

        [JsonPropertyName("public")]
        public decimal? Public
        {
            get => _publicHolding;
            set => _publicHolding = value;
        }

        [JsonIgnore]
        public decimal? PublicHolding
        {
            get => _publicHolding;
            set => _publicHolding = value;
        }

        [JsonPropertyName("employee_trust_pct")]
        public decimal? EmployeeTrustPct { get; set; }

        [JsonPropertyName("mutual_fund_pct")]
        public decimal? MutualFundPct { get; set; }

        [JsonPropertyName("as_on_date")]
        public string? AsOnDate { get; set; }

        [JsonPropertyName("period_date")]
        public string? PeriodDateString { get; set; }

        [JsonPropertyName("date")]
        public string? DateString { get; set; }

        [JsonPropertyName("period")]
        public string? Period { get; set; }

        [JsonPropertyName("quarter")]
        public string? Quarter { get; set; }

        [JsonPropertyName("period_type")]
        public string? PeriodType { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>
        /// Resolved parsed Date for financial Data As Of date.
        /// </summary>
        [JsonIgnore]
        public DateTime? ResolvedPeriodDate
        {
            get
            {
                var candidate = AsOnDate ?? PeriodDateString ?? DateString;
                if (!string.IsNullOrWhiteSpace(candidate) &&
                    DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
                {
                    return dt.Date;
                }
                return null;
            }
        }

        /// <summary>
        /// Resolved human-readable period label (e.g. "Q1 FY2026").
        /// </summary>
        [JsonIgnore]
        public string ResolvedPeriod
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Period)) return Period.Trim();
                if (!string.IsNullOrWhiteSpace(Quarter)) return Quarter.Trim();
                if (ResolvedPeriodDate.HasValue)
                {
                    var d = ResolvedPeriodDate.Value;
                    return $"{d:MMM yyyy}";
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Mandatory, deterministic PeriodKey for database unique indexing and idempotent upsert.
        /// Returns empty string if invalid/cannot be resolved.
        /// </summary>
        [JsonIgnore]
        public string ResolvedPeriodKey
        {
            get
            {
                if (ResolvedPeriodDate.HasValue)
                {
                    return ResolvedPeriodDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                var p = ResolvedPeriod;
                if (!string.IsNullOrWhiteSpace(p))
                {
                    // Normalize standard quarterly patterns e.g. "Q1 FY2026" -> "2026-Q1"
                    var match = Regex.Match(p, @"Q([1-4])\s*(?:FY)?\s*(\d{4})", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var q = match.Groups[1].Value;
                        var y = match.Groups[2].Value;
                        return $"{y}-Q{q}";
                    }

                    // Fallback to normalized uppercase alphanumeric string
                    var clean = Regex.Replace(p.ToUpperInvariant(), @"[^A-Z0-9]", "-").Trim('-');
                    return clean;
                }

                return string.Empty;
            }
        }
    }

    public class BharatStockApiResponseWrapper
    {
        [JsonPropertyName("ticker")]
        public string? Ticker { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("shareholding")]
        public List<BharatStockShareholdingRecord>? Shareholding { get; set; }

        [JsonPropertyName("data")]
        public List<BharatStockShareholdingRecord>? Data { get; set; }

        [JsonPropertyName("history")]
        public List<BharatStockShareholdingRecord>? History { get; set; }
    }
}
