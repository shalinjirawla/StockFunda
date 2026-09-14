using System.Collections.Generic;

namespace StockLens_BusinessLayer.DTOs
{
    public class PriceHistoryResponseDto
    {
        public string Symbol { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        // X-axis dates
        public List<string> Dates { get; set; } = new();

        // Arrays for chart series
        public List<decimal> ClosePrices { get; set; } = new();
        public List<long> Volumes { get; set; } = new();

        // Additional data if High-Low-Open is needed
        public List<decimal> OpenPrices { get; set; } = new();
        public List<decimal> HighPrices { get; set; } = new();
        public List<decimal> LowPrices { get; set; } = new();
        
        public string Source { get; set; } = "IndianAPI";
        public string LastSyncedAt { get; set; } = string.Empty;
    }
}
