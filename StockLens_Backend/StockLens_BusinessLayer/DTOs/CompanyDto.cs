namespace StockLens_BusinessLayer.DTOs
{
    public class CompanyDto
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string? Industry { get; set; }
        public string? LogoUrl { get; set; }
    }
}
