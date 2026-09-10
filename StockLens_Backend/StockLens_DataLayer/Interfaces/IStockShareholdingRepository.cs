using StockLens_DataLayer.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockLens_DataLayer.Interfaces
{
    public interface IStockShareholdingRepository
    {
        Task<List<StockShareholding>> GetShareholdingsByStockIdAsync(int stockId, int limit = 12);
        Task<StockShareholding?> GetLatestShareholdingByStockIdAsync(int stockId);
        Task<StockShareholding?> GetByStockIdAndPeriodKeyAsync(int stockId, string periodKey);
        Task<DateTime?> GetLatestSyncedAtByStockIdAsync(int stockId);
        Task AddAsync(StockShareholding shareholding);
        Task AddRangeAsync(IEnumerable<StockShareholding> shareholdings);
        Task UpdateAsync(StockShareholding shareholding);
        Task<int> SaveChangesAsync();
    }
}
