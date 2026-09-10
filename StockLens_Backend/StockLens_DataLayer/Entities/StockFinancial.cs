using System;

namespace StockLens_DataLayer.Entities
{
    public class StockFinancial
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
        /// Fiscal year identifier (e.g. "FY25", "FY24").
        /// </summary>
        public string FiscalYear { get; set; } = string.Empty;

        /// <summary>
        /// Exact period end date (e.g. 2025-03-31).
        /// </summary>
        public DateTime? PeriodEndDate { get; set; }

        /// <summary>
        /// Annual Revenue / Topline in ₹ Crores (or normalized unit from XBRL).
        /// </summary>
        public decimal? Revenue { get; set; }

        /// <summary>
        /// Annual Net Profit / Bottomline after tax.
        /// </summary>
        public decimal? NetProfit { get; set; }

        /// <summary>
        /// Diluted / Basic Earnings Per Share.
        /// </summary>
        public decimal? Eps { get; set; }

        /// <summary>
        /// Net profit attributable to minority interest.
        /// </summary>
        public decimal? NetProfitAttributableToMinorityInterest { get; set; }

        /// <summary>
        /// Ind-AS reserves + retained earnings (used for Altman Z-score balance sheet strength).
        /// </summary>
        public decimal? OtherEquity { get; set; }

        /// <summary>
        /// Cash Flow from Operating Activities (CFO / cash_flow_operating).
        /// </summary>
        public decimal? OperatingCashFlow { get; set; }

        /// <summary>
        /// Capital Expenditure (Purchase of PPE + intangibles).
        /// </summary>
        public decimal? Capex { get; set; }

        /// <summary>
        /// Free Cash Flow = OperatingCashFlow - Capex.
        /// </summary>
        public decimal? FreeCashFlow { get; set; }

        /// <summary>
        /// Net Cash Flow across all activities (Operating + Investing + Financing), when available from API.
        /// </summary>
        public decimal? NetCashFlow { get; set; }

        /// <summary>
        /// Filing consolidation type: "consolidated" or "standalone".
        /// </summary>
        public string? ConsolidationType { get; set; }

        public string Source { get; set; } = "BharatStock";
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
