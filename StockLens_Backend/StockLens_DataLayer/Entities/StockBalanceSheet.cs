using System;

namespace StockLens_DataLayer.Entities
{
    public class StockBalanceSheet
    {
        public int Id { get; set; }
        public int StockId { get; set; }
        public Stock Stock { get; set; } = null!;

        /// <summary>
        /// Deterministic unique key (e.g., "annual-FY25" or "annual-2025-03-31") for idempotent upserts.
        /// </summary>
        public string PeriodKey { get; set; } = string.Empty;

        /// <summary>
        /// Reporting period type: "annual" or "quarterly".
        /// </summary>
        public string PeriodType { get; set; } = "annual";

        /// <summary>
        /// Fiscal year identifier (e.g. "FY25", "FY24" or "Mar 2024").
        /// </summary>
        public string FiscalYear { get; set; } = string.Empty;

        /// <summary>
        /// Exact period end date.
        /// </summary>
        public DateTime? PeriodEndDate { get; set; }

        /// <summary>
        /// Filing consolidation type: "consolidated" or "standalone".
        /// </summary>
        public string? ConsolidationType { get; set; }

        // Liabilities Side
        public decimal? EquityCapital { get; set; }
        public decimal? Reserves { get; set; }
        public decimal? Borrowings { get; set; }
        public decimal? OtherLiabilities { get; set; }
        public decimal? TotalLiabilities { get; set; }

        // Assets Side
        public decimal? FixedAssets { get; set; }
        public decimal? Cwip { get; set; }
        public decimal? Investments { get; set; }
        public decimal? OtherAssets { get; set; }
        public decimal? TotalAssets { get; set; }

        public string Source { get; set; } = "IndianAPI";
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total Equity (Shareholders' Funds) = Total Assets - (Borrowings + Other Liabilities)
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal? CalculatedTotalEquity =>
            TotalAssets.HasValue
                ? TotalAssets.Value - ((Borrowings ?? 0m) + (OtherLiabilities ?? 0m))
                : (EquityCapital.HasValue || Reserves.HasValue ? (EquityCapital ?? 0m) + (Reserves ?? 0m) : (decimal?)null);
    }
}
