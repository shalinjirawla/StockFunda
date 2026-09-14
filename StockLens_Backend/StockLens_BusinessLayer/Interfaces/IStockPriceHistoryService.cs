using System.Threading;
using System.Threading.Tasks;
using StockLens_BusinessLayer.DTOs;

namespace StockLens_BusinessLayer.Interfaces
{
    public interface IStockPriceHistoryService
    {
        Task<PriceHistoryResponseDto> GetPriceHistoryByStockIdAsync(int stockId, bool forceRefresh = false, CancellationToken cancellationToken = default);
        Task<PriceHistoryResponseDto> GetPriceHistoryBySymbolAsync(string symbol, string? exchange = null, bool forceRefresh = false, CancellationToken cancellationToken = default);
    }
}
