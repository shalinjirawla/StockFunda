using System;
using System.Collections.Generic;

namespace StockLens_DataLayer.Entities
{
    public class Stock
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Exchange { get; set; } = "NSE"; // NSE or BSE
        public string? Industry { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<StockNews> News { get; set; } = new List<StockNews>();
    }
}
