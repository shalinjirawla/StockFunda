using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StockLens_DataLayer.Entities;

namespace StockLens_DataLayer.Interfaces
{
    public interface IStockPriceHistoryRepository
    {
        Task<IReadOnlyList<StockPriceHistory>> GetByStockIdAsync(int stockId, CancellationToken cancellationToken = default);
        Task<StockPriceHistory?> GetLatestByStockIdAsync(int stockId, CancellationToken cancellationToken = default);
        Task AddRangeAsync(IEnumerable<StockPriceHistory> prices, CancellationToken cancellationToken = default);
        Task RemoveRangeAsync(IEnumerable<StockPriceHistory> prices, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
