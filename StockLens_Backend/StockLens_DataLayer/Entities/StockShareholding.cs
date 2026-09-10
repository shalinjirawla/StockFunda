using System;

namespace StockLens_DataLayer.Entities
{
    public class StockShareholding
    {
        public int Id { get; set; }
        public int StockId { get; set; }
        public Stock Stock { get; set; } = null!;

        /// <summary>
        /// Normalized deterministic period key (e.g. "2025-06-30" or "2026-Q1") for unique indexing and idempotent upsert.
        /// </summary>
        public string PeriodKey { get; set; } = string.Empty;

        /// <summary>
        /// Display label for reporting period (e.g. "Q1 FY2026", "Jun 2025").
        /// </summary>
        public string Period { get; set; } = string.Empty;

        /// <summary>
        /// Financial Data As Of reporting date for chronological sorting.
        /// </summary>
        public DateTime? PeriodDate { get; set; }

        /// <summary>
        /// Period type as reported by provider (e.g. "Quarterly").
        /// </summary>
        public string? PeriodType { get; set; }

        public decimal? PromoterHolding { get; set; }
        public decimal? FiiHolding { get; set; }
        public decimal? DiiHolding { get; set; }
        public decimal? GovernmentHolding { get; set; }
        public decimal? PublicHolding { get; set; }
        public decimal? OtherHolding { get; set; }
        public long? ShareholdersCount { get; set; }

        public string Source { get; set; } = "IndianAPI";
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
