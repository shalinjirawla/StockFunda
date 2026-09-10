using System;
using System.Collections.Generic;

namespace StockLens_DataLayer.Entities
{
    public class Company
    {
        public int Id { get; set; }
        
        // Static Company Information
        public string CompanyName { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string? Industry { get; set; }
        public string? LogoUrl { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property: Ek company ke multiple stocks ho sakte hain
        public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    }
}
