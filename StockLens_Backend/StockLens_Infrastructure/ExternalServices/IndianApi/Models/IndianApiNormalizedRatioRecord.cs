using System;

namespace StockLens_Infrastructure.ExternalServices.IndianApi.Models
{
    public class IndianApiNormalizedRatioRecord
    {
        public string Period { get; set; } = string.Empty;
        public string PeriodKey { get; set; } = string.Empty;
        public DateTime? PeriodDate { get; set; }
        public string PeriodType { get; set; } = "Annual";

        public decimal? DebtorDays { get; set; }
        public decimal? InventoryDays { get; set; }
        public decimal? PayableDays { get; set; }
        public decimal? CashConversionCycle { get; set; }
        public decimal? WorkingCapitalDays { get; set; }
        public decimal? RocePercentage { get; set; }

        public string Source { get; set; } = "IndianAPI";
    }
}
