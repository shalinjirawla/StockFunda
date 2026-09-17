using Microsoft.EntityFrameworkCore;
using StockLens_DataLayer.Entities;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.DataContext;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.Repositories
{
    public class StockBalanceSheetRepository : IStockBalanceSheetRepository
    {
        private readonly StockLensDataContext _context;

        public StockBalanceSheetRepository(StockLensDataContext context)
        {
            _context = context;
        }

        public async Task<List<StockBalanceSheet>> GetRecentByStockIdAsync(int stockId, int limit = 5)
        {
            return await _context.StockBalanceSheets
                .Where(b => b.StockId == stockId)
                .OrderByDescending(b => b.PeriodKey)
                .Take(limit)
                .ToListAsync();
        }

        public Task RemoveRangeAsync(IEnumerable<StockBalanceSheet> balanceSheets)
        {
            _context.StockBalanceSheets.RemoveRange(balanceSheets);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<StockBalanceSheet> balanceSheets)
        {
            _context.StockBalanceSheets.AddRange(balanceSheets);
            return Task.CompletedTask;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
