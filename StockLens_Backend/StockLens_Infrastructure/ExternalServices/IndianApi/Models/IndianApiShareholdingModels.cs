using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StockLens_Infrastructure.ExternalServices.IndianApi.Models
{
    public class IndianApiNormalizedQuarterRecord
    {
        public string Period { get; set; } = string.Empty;
        public string PeriodKey { get; set; } = string.Empty;
        public DateTime? PeriodDate { get; set; }
        public string PeriodType { get; set; } = "Quarterly";

        public decimal? PromoterHolding { get; set; }
        public decimal? FiiHolding { get; set; }
        public decimal? DiiHolding { get; set; }
        public decimal? GovernmentHolding { get; set; }
        public decimal? PublicHolding { get; set; }
        public decimal? OtherHolding { get; set; }
        public long? ShareholdersCount { get; set; }

        public string Source { get; set; } = "IndianAPI";

        /// <summary>
        /// Resolves any period string (e.g. "Jun 2026", "Sept 2025", "2026-06-30") to a deterministic period-end PeriodKey and Date.
        /// </summary>
        public static (string PeriodKey, DateTime? PeriodDate, string NormalizedPeriod) ResolvePeriod(string rawPeriod)
        {
            if (string.IsNullOrWhiteSpace(rawPeriod))
            {
                return (string.Empty, null, string.Empty);
            }

            var trimmed = rawPeriod.Trim();

            // Normalize variants like "Sept 2025" -> "Sep 2025"
            var cleanPeriod = Regex.Replace(trimmed, @"\bSept\b", "Sep", RegexOptions.IgnoreCase);

            // Try exact standard Month Year (e.g. "Jun 2026", "September 2025")
            string[] formats = new[]
            {
                "MMM yyyy", "MMMM yyyy", "MMM-yyyy", "MMM/yyyy",
                "yyyy-MM-dd", "yyyy/MM/dd", "dd-MMM-yyyy", "dd MMM yyyy"
            };

            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(cleanPeriod, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    // If parsed format had no specific day (or day was 1), calculate end-of-month date
                    int day = dt.Day;
                    if (fmt.StartsWith("MMM") || fmt.StartsWith("MMMM"))
                    {
                        day = DateTime.DaysInMonth(dt.Year, dt.Month);
                    }

                    var endOfMonthDate = new DateTime(dt.Year, dt.Month, day, 0, 0, 0, DateTimeKind.Utc);
                    var key = endOfMonthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    var label = endOfMonthDate.ToString("MMM yyyy", CultureInfo.InvariantCulture);
                    return (key, endOfMonthDate, label);
                }
            }

            // General DateTime parse fallback
            if (DateTime.TryParse(cleanPeriod, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallbackDt))
            {
                int day = DateTime.DaysInMonth(fallbackDt.Year, fallbackDt.Month);
                var endOfMonthDate = new DateTime(fallbackDt.Year, fallbackDt.Month, day, 0, 0, 0, DateTimeKind.Utc);
                var key = endOfMonthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return (key, endOfMonthDate, endOfMonthDate.ToString("MMM yyyy", CultureInfo.InvariantCulture));
            }

            // Regex for patterns like "Q1 2026" or "2026-Q1"
            var qMatch = Regex.Match(cleanPeriod, @"Q([1-4])\s*(?:FY)?\s*(\d{4})|(\d{4})\s*-?\s*Q([1-4])", RegexOptions.IgnoreCase);
            if (qMatch.Success)
            {
                int quarter = int.Parse(qMatch.Groups[1].Success ? qMatch.Groups[1].Value : qMatch.Groups[4].Value, CultureInfo.InvariantCulture);
                int year = int.Parse(qMatch.Groups[2].Success ? qMatch.Groups[2].Value : qMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                int month = quarter * 3; // Q1 -> Mar(3), Q2 -> Jun(6), Q3 -> Sep(9), Q4 -> Dec(12)
                int lastDay = DateTime.DaysInMonth(year, month);
                var qDate = new DateTime(year, month, lastDay, 0, 0, 0, DateTimeKind.Utc);
                return (qDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), qDate, qDate.ToString("MMM yyyy", CultureInfo.InvariantCulture));
            }

            // Fallback normalized alphanumeric key
            var safeKey = Regex.Replace(trimmed.ToUpperInvariant(), @"[^A-Z0-9]", "-").Trim('-');
            return (safeKey, null, trimmed);
        }
    }
}
