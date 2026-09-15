using StockLens_BusinessLayer.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Interfaces
{
    public interface IStockCashflowService
    {
        Task<StockCashflowResponseDto> GetCashflowByStockIdAsync(
            int stockId,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);

        Task<StockCashflowResponseDto> GetCashflowBySymbolAsync(
            string symbol,
            string? exchange = "NSE",
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);

        Task<StockRatiosDto?> GetRatiosBySymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default);
    }
}
