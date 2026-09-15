using StockLens_BusinessLayer.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Interfaces
{
    public interface IStockQuarterlyResultsService
    {
        Task<StockQuarterlyResultsResponseDto> GetQuarterlyResultsByStockIdAsync(
            int stockId,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);

        Task<StockQuarterlyResultsResponseDto> GetQuarterlyResultsBySymbolAsync(
            string symbol,
            string? exchange = "NSE",
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);
    }
}
