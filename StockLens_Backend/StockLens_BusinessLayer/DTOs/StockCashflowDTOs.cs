using System;
using System.Collections.Generic;

namespace StockLens_BusinessLayer.DTOs
{
    public class StockCashflowResponseDto
    {
        public int StockId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Exchange { get; set; } = "NSE";
        public string CompanyName { get; set; } = string.Empty;
        public string LatestFiscalYear { get; set; } = string.Empty;
        public string? DataAsOf { get; set; }
        public string Source { get; set; } = "BharatStock";
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;

        public CashflowSummaryDto Summary { get; set; } = new();
        public List<AnnualCashflowItemDto> History { get; set; } = new();
    }

    public class CashflowSummaryDto
    {
        public string FiscalYear { get; set; } = string.Empty;
        public string? PeriodEndDate { get; set; }
        public decimal? OperatingCashFlow { get; set; } // CFO
        public decimal? Capex { get; set; }
        public decimal? FreeCashFlow { get; set; } // FCF = CFO - Capex
        public decimal? NetCashFlow { get; set; } // Net Cash Flow (when provided by API)
        public decimal? Revenue { get; set; }
        public decimal? NetProfit { get; set; }
        public decimal? Eps { get; set; }
        public decimal? OtherEquity { get; set; }
        public decimal? CfoToNetProfitRatio { get; set; } // CFO / Net Profit quality ratio
        public decimal? FcfMarginPercent { get; set; } // FCF / Revenue * 100
        public decimal? CapexToCfoPercent { get; set; } // Capex / CFO * 100
        public string? ConsolidationType { get; set; } = "consolidated";

        public CashflowYoYChangeDto YoYChange { get; set; } = new();
    }

    public class CashflowYoYChangeDto
    {
        public decimal? FreeCashFlowChange { get; set; }
        public decimal? FreeCashFlowGrowth { get; set; } // YoY %

        public decimal? OperatingCashFlowChange { get; set; }
        public decimal? OperatingCashFlowGrowth { get; set; } // YoY %

        public decimal? CapexChange { get; set; }
        public decimal? CapexGrowth { get; set; } // YoY %

        public decimal? NetProfitChange { get; set; }
        public decimal? NetProfitGrowth { get; set; } // YoY %

        public decimal? RevenueChange { get; set; }
        public decimal? RevenueGrowth { get; set; } // YoY %

        public decimal? NetCashFlowChange { get; set; }
        public decimal? NetCashFlowGrowth { get; set; } // YoY %
    }

    public class AnnualCashflowItemDto
    {
        public int Id { get; set; }
        public string FiscalYear { get; set; } = string.Empty;
        public string? PeriodKey { get; set; }
        public string? PeriodEndDate { get; set; }
        public string? DataAsOf { get; set; }
        public string PeriodType { get; set; } = "annual";
        public decimal? Revenue { get; set; }
        public decimal? NetProfit { get; set; }
        public decimal? Eps { get; set; }
        public decimal? NetProfitAttributableToMinorityInterest { get; set; }
        public decimal? OtherEquity { get; set; }
        public decimal? OperatingCashFlow { get; set; }
        public decimal? Capex { get; set; }
        public decimal? FreeCashFlow { get; set; }
        public decimal? NetCashFlow { get; set; }
        public decimal? CfoToNetProfitRatio { get; set; }
        public decimal? FcfMarginPercent { get; set; }
        public decimal? CapexToCfoPercent { get; set; }
        public string? ConsolidationType { get; set; }
        public string Source { get; set; } = "BharatStock";
        public DateTime LastSyncedAt { get; set; }
    }
}
