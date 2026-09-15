using System;
using System.Collections.Generic;

namespace StockLens_BusinessLayer.DTOs
{
    public class StockQuarterlyResultsResponseDto
    {
        public int StockId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Exchange { get; set; } = "NSE";
        public string CompanyName { get; set; } = string.Empty;
        public string LatestQuarter { get; set; } = string.Empty;
        public string Source { get; set; } = "IndianAPI";
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Latest quarter record (Revenue, Net Profit, EPS, Tax, Depreciation, etc.)
        /// </summary>
        public QuarterlyRecordDto Summary { get; set; } = new();

        /// <summary>
        /// Quarter-over-Quarter (QoQ) growth compared to previous immediate quarter.
        /// </summary>
        public QuarterlyGrowthDto QoQGrowth { get; set; } = new();

        /// <summary>
        /// Year-over-Year (YoY) growth compared to the same quarter last fiscal year.
        /// </summary>
        public QuarterlyGrowthDto YoYGrowth { get; set; } = new();

        /// <summary>
        /// Chronological historical quarter records (newest to oldest or oldest to newest for charting).
        /// </summary>
        public List<QuarterlyRecordDto> History { get; set; } = new();
    }

    public class QuarterlyRecordDto
    {
        public string PeriodKey { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public DateTime? PeriodEndDate { get; set; }

        /// <summary>
        /// Sales / Topline Revenue in ₹ Crores
        /// </summary>
        public decimal? Sales { get; set; }

        /// <summary>
        /// Total Expenses in ₹ Crores
        /// </summary>
        public decimal? Expenses { get; set; }

        /// <summary>
        /// Operating Profit in ₹ Crores
        /// </summary>
        public decimal? OperatingProfit { get; set; }

        /// <summary>
        /// Operating Profit Margin % (OPM %)
        /// </summary>
        public decimal? OpmPercentage { get; set; }

        /// <summary>
        /// Other non-operating income in ₹ Crores
        /// </summary>
        public decimal? OtherIncome { get; set; }

        /// <summary>
        /// Interest expense in ₹ Crores
        /// </summary>
        public decimal? Interest { get; set; }

        /// <summary>
        /// Depreciation & Amortization in ₹ Crores
        /// </summary>
        public decimal? Depreciation { get; set; }

        /// <summary>
        /// Profit Before Tax (PBT) in ₹ Crores
        /// </summary>
        public decimal? ProfitBeforeTax { get; set; }

        /// <summary>
        /// Provision for Income Tax in ₹ Crores
        /// </summary>
        public decimal? Tax { get; set; }

        /// <summary>
        /// Effective Tax rate %
        /// </summary>
        public decimal? TaxPercentage { get; set; }

        /// <summary>
        /// Net Profit after Tax (PAT / Bottomline) in ₹ Crores
        /// </summary>
        public decimal? NetProfit { get; set; }

        /// <summary>
        /// Earnings Per Share (EPS) in ₹
        /// </summary>
        public decimal? Eps { get; set; }

        /// <summary>
        /// Consolidation status ("consolidated" or "standalone")
        /// </summary>
        public string? ConsolidationType { get; set; } = "consolidated";
    }

    public class QuarterlyGrowthDto
    {
        public decimal? SalesGrowthPercent { get; set; }
        public decimal? OperatingProfitGrowthPercent { get; set; }
        public decimal? NetProfitGrowthPercent { get; set; }
        public decimal? EpsGrowthPercent { get; set; }
        public decimal? DepreciationGrowthPercent { get; set; }
        public decimal? TaxGrowthPercent { get; set; }
    }
}
