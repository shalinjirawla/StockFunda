using System.Collections.Generic;

namespace StockLens_BusinessLayer.Models
{
    public class BalanceSheetResponseDto
    {
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// Ordered list of periods, e.g. ["Mar 2018", "Mar 2019", "Mar 2020", ...]
        /// </summary>
        public List<string> Periods { get; set; } = new();

        /// <summary>
        /// List of line items (rows) for the balance sheet.
        /// </summary>
        public List<BalanceSheetLineItemDto> LineItems { get; set; } = new();
        
        public decimal? AssetGrowthPercentage { get; set; }

        public string? ConsolidationType { get; set; }
        public string? LatestPeriodEnd { get; set; }
        public string? Source { get; set; }
        public string? LastSyncedAt { get; set; }

        public string? ErrorMessage { get; set; }
    }

    public class BalanceSheetLineItemDto
    {
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicates if this row is a total (e.g. Total Assets, Total Liabilities) to highlight it in UI.
        /// </summary>
        public bool IsTotal { get; set; }

        /// <summary>
        /// Values corresponding to the periods array in the parent DTO.
        /// Can contain nulls if data is missing for a specific year.
        /// </summary>
        public List<decimal?> Values { get; set; } = new();
    }
}
