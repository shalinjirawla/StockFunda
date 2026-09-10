namespace StockLens_Infrastructure.ExternalServices.BharatStock
{
    public class BharatStockSettings
    {
        public const string SectionName = "BharatStock";

        public string BaseUrl { get; set; } = "https://bharatstockapi.com";
        public string ApiKey { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 15;
        public bool UseMockData { get; set; } = false;
    }
}
