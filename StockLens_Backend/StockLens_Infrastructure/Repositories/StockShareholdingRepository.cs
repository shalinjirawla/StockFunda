using Microsoft.EntityFrameworkCore;
using StockLens_DataLayer.Entities;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.DataContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.Repositories
{
    public class StockShareholdingRepository : IStockShareholdingRepository
    {
        private readonly StockLensDataContext _context;

        public StockShareholdingRepository(StockLensDataContext context)
        {
            _context = context;
        }

        public async Task<List<StockShareholding>> GetShareholdingsByStockIdAsync(int stockId, int limit = 12)
        {
            if (limit < 1) limit = 12;
            if (limit > 20) limit = 20;

            return await _context.StockShareholdings
                .AsNoTracking()
                .Where(s => s.StockId == stockId)
                .OrderByDescending(s => s.PeriodDate)
                .ThenByDescending(s => s.PeriodKey)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<StockShareholding?> GetLatestShareholdingByStockIdAsync(int stockId)
        {
            return await _context.StockShareholdings
                .AsNoTracking()
                .Where(s => s.StockId == stockId)
                .OrderByDescending(s => s.PeriodDate)
                .ThenByDescending(s => s.PeriodKey)
                .FirstOrDefaultAsync();
        }

        public async Task<StockShareholding?> GetByStockIdAndPeriodKeyAsync(int stockId, string periodKey)
        {
            if (string.IsNullOrWhiteSpace(periodKey)) return null;

            return await _context.StockShareholdings
                .FirstOrDefaultAsync(s => s.StockId == stockId && s.PeriodKey == periodKey);
        }

        public async Task<DateTime?> GetLatestSyncedAtByStockIdAsync(int stockId)
        {
            return await _context.StockShareholdings
                .AsNoTracking()
                .Where(s => s.StockId == stockId)
                .OrderByDescending(s => s.LastSyncedAt)
                .Select(s => (DateTime?)s.LastSyncedAt)
                .FirstOrDefaultAsync();
        }

        public async Task AddAsync(StockShareholding shareholding)
        {
            await _context.StockShareholdings.AddAsync(shareholding);
        }

        public async Task AddRangeAsync(IEnumerable<StockShareholding> shareholdings)
        {
            await _context.StockShareholdings.AddRangeAsync(shareholdings);
        }

        public Task UpdateAsync(StockShareholding shareholding)
        {
            _context.StockShareholdings.Update(shareholding);
            return Task.CompletedTask;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
