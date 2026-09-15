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
        /// Annual Operating Profit (EBIT / operating_profit).
        /// </summary>
        public decimal? OperatingProfit { get; set; }

        /// <summary>
        /// Annual Net Profit / Bottomline after tax.
        /// </summary>
        public decimal? NetProfit { get; set; }

        /// <summary>
        /// Diluted / Basic Earnings Per Share.
        /// </summary>
        public decimal? Eps { get; set; }

        /// <summary>
        /// Trailing 12 Months (TTM) Earnings Per Share used for P/E valuation.
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal? TtmEps { get; set; }

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

        /// <summary>
        /// Return on Equity (ROE %) from latest ratios endpoint.
        /// </summary>
        public decimal? Roe { get; set; }

        /// <summary>
        /// Return on Capital Employed (ROCE %) from latest ratios endpoint.
        /// </summary>
        public decimal? Roce { get; set; }

        /// <summary>
        /// Price-to-Earnings (P/E) ratio.
        /// </summary>
        public decimal? PeRatio { get; set; }

        /// <summary>
        /// Price-to-Book (P/B) ratio.
        /// </summary>
        public decimal? PbRatio { get; set; }

        /// <summary>
        /// Dividend Yield (%).
        /// </summary>
        public decimal? DividendYield { get; set; }

        /// <summary>
        /// 52-Week High price.
        /// </summary>
        public decimal? Week52High { get; set; }

        /// <summary>
        /// 52-Week Low price.
        /// </summary>
        public decimal? Week52Low { get; set; }

        /// <summary>
        /// Latest traded stock price used for ratio calculations.
        /// </summary>
        public decimal? CurrentPrice { get; set; }

        /// <summary>
        /// As of date for the ratios data (e.g. 2026-08-12).
        /// </summary>
        public string? RatiosAsOfDate { get; set; }

        /// <summary>
        /// Face Value (Nominal per share value from company overview).
        /// </summary>
        public decimal? FaceValue { get; set; }

        /// <summary>
        /// Equity Capital from Balance Sheet (in ₹ Crores).
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal? EquityCapital { get; set; }

        /// <summary>
        /// Total Number of Shares = Equity Capital / Face Value (in ₹ Crores shares).
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal? TotalShares { get; set; }

        /// <summary>
        /// Total Equity / Shareholders' Funds (in ₹ Crores).
        /// </summary>
        public decimal? TotalEquity { get; set; }

        /// <summary>
        /// Source metadata for Total Equity: "Reported" vs "Calculated (Total Assets - Total Liabilities)"
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? TotalEquitySource { get; set; }

        /// <summary>
        /// Book Value Per Share (from screener).
        /// </summary>
        public decimal? BookValue { get; set; }

        /// <summary>
        /// Market Capitalization in ₹ Crores (from screener or calculated: Total Shares * Current Price).
        /// </summary>
        public decimal? MarketCap { get; set; }

        /// <summary>
        /// Source metadata for Market Cap (e.g. Calculated vs Reported).
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? MarketCapSource { get; set; }

        /// <summary>
        /// Sector P/E (Industry Average Valuation Multiple benchmark).
        /// </summary>
        public decimal? SectorPe { get; set; }

        /// <summary>
        /// Sector name metadata for Sector P/E benchmark.
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? SectorPeSector { get; set; }

        public string Source { get; set; } = "BharatStock";
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}


