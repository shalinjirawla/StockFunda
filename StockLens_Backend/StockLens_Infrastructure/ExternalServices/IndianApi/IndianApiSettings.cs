namespace StockLens_Infrastructure.ExternalServices.IndianApi
{
    public class IndianApiSettings
    {
        public const string SectionName = "IndianApi";

        public string BaseUrl { get; set; } = "https://stock.indianapi.in";
        public string ApiKey { get; set; } = string.Empty;
        public int NewsCacheMinutes { get; set; } = 10;
        public int TimeoutSeconds { get; set; } = 15;
    }
}
