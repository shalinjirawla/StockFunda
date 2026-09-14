using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockLens_DataLayer.Entities;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.DataContext;

namespace StockLens_Infrastructure.Repositories
{
    public class StockPriceHistoryRepository : IStockPriceHistoryRepository
    {
        private readonly StockLensDataContext _context;

        public StockPriceHistoryRepository(StockLensDataContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<StockPriceHistory>> GetByStockIdAsync(int stockId, CancellationToken cancellationToken = default)
        {
            return await _context.StockPriceHistories
                .Where(p => p.StockId == stockId)
                .OrderBy(p => p.Date)
                .ToListAsync(cancellationToken);
        }

        public async Task<StockPriceHistory?> GetLatestByStockIdAsync(int stockId, CancellationToken cancellationToken = default)
        {
            return await _context.StockPriceHistories
                .Where(p => p.StockId == stockId)
                .OrderByDescending(p => p.Date)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task AddRangeAsync(IEnumerable<StockPriceHistory> prices, CancellationToken cancellationToken = default)
        {
            await _context.StockPriceHistories.AddRangeAsync(prices, cancellationToken);
        }

        public Task RemoveRangeAsync(IEnumerable<StockPriceHistory> prices, CancellationToken cancellationToken = default)
        {
            _context.StockPriceHistories.RemoveRange(prices);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
