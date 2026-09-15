using System;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Interfaces
{
    public class SectorValuationResultDto
    {
        public string Sector { get; set; } = string.Empty;
        public decimal? SectorPe { get; set; }
        public decimal? TotalMarketCapCr { get; set; }
        public decimal? TotalNetProfitCr { get; set; }
        public int EligibleCompaniesCount { get; set; }
        public int ExcludedCompaniesCount { get; set; }
        public string? AsOfDate { get; set; }
        public string CalculationMethod { get; set; } = "Aggregate Market Cap / Aggregate Positive TTM Earnings";
    }

    public interface ISectorValuationService
    {
        Task<SectorValuationResultDto> GetSectorValuationAsync(
            string sector,
            string? exchange = "NSE",
            CancellationToken cancellationToken = default);
    }
}
