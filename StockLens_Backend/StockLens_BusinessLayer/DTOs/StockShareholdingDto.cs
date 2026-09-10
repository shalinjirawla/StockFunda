using System;
using System.Collections.Generic;

namespace StockLens_BusinessLayer.DTOs
{
    public class ShareholdingPeriodDto
    {
        public string Period { get; set; } = string.Empty;
        public string PeriodKey { get; set; } = string.Empty;
        public DateTime? PeriodDate { get; set; }
        public string? PeriodType { get; set; }
        public decimal? Promoter { get; set; }
        public decimal? Fii { get; set; }
        public decimal? Dii { get; set; }
        public decimal? Government { get; set; }
        public decimal? Public { get; set; }
        public decimal? Others { get; set; }
        public long? ShareholdersCount { get; set; }
        public decimal? Total => (Promoter.HasValue || Fii.HasValue || Dii.HasValue || Government.HasValue || Public.HasValue || Others.HasValue)
            ? Math.Round((Promoter ?? 0m) + (Fii ?? 0m) + (Dii ?? 0m) + (Government ?? 0m) + (Public ?? 0m) + (Others ?? 0m), 2)
            : null;
        public string? DataAsOf { get; set; }
    }

    /// <summary>
    /// Represents absolute percentage-point movement (current - previous).
    /// Example: 50.25% - 49.80% = +0.45 percentage points.
    /// </summary>
    public class ShareholdingChangeDto
    {
        public decimal? Promoter { get; set; }
        public decimal? Fii { get; set; }
        public decimal? Dii { get; set; }
        public decimal? Government { get; set; }
        public decimal? Public { get; set; }
        public decimal? Others { get; set; }
    }

    /// <summary>
    /// Represents relative percentage change ((current - previous) / previous * 100).
    /// Example: ((50.25 - 49.80) / 49.80) * 100 = +0.90%.
    /// </summary>
    public class ShareholdingRelativeChangeDto
    {
        public decimal? Promoter { get; set; }
        public decimal? Fii { get; set; }
        public decimal? Dii { get; set; }
        public decimal? Government { get; set; }
        public decimal? Public { get; set; }
        public decimal? Others { get; set; }
    }

    public class ShareholdingValidationSummaryDto
    {
        public bool IsValid { get; set; } = true;
        public decimal TotalPercentage { get; set; }
        public string? Notes { get; set; }
    }

    public class StockShareholdingResponseDto
    {
        public int StockId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Exchange { get; set; } = "NSE";
        public string CompanyName { get; set; } = string.Empty;

        public ShareholdingPeriodDto CurrentPeriod { get; set; } = null!;
        public ShareholdingPeriodDto? PreviousPeriod { get; set; }

        /// <summary>
        /// Percentage-point change (+0.45 pp, -0.90 pp).
        /// </summary>
        public ShareholdingChangeDto Change { get; set; } = new();

        /// <summary>
        /// Relative percentage change (+0.90%, -4.71%).
        /// </summary>
        public ShareholdingRelativeChangeDto RelativeChange { get; set; } = new();

        /// <summary>
        /// Historical periods for trend analysis and chart visualization.
        /// </summary>
        public List<ShareholdingPeriodDto> History { get; set; } = new();

        public string? DataAsOf { get; set; }
        public string Source { get; set; } = "IndianAPI";
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
        public ShareholdingValidationSummaryDto? Validation { get; set; }
    }
}
