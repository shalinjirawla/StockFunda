using StockLens_BusinessLayer.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Interfaces
{
    public interface IStockShareholdingService
    {
        Task<StockShareholdingResponseDto> GetShareholdingByStockIdAsync(
            int stockId,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);

        Task<StockShareholdingResponseDto> GetShareholdingBySymbolAsync(
            string symbol,
            string? exchange = "NSE",
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);
    }
}
