namespace StockLens_BusinessLayer.Models
{
    public class AssetGrowthDto
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal? CurrentAssets { get; set; }
        public decimal? PreviousAssets { get; set; }
        public decimal? GrowthPercentage { get; set; }
        public bool IsPositive { get; set; }
        public string Period { get; set; } = "YoY";
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
