using StockLens_DataLayer.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockLens_DataLayer.Interfaces
{
    public interface IStockBalanceSheetRepository
    {
        Task<List<StockBalanceSheet>> GetRecentByStockIdAsync(int stockId, int limit = 3);
        Task RemoveRangeAsync(IEnumerable<StockBalanceSheet> balanceSheets);
        Task AddRangeAsync(IEnumerable<StockBalanceSheet> balanceSheets);
        Task<int> SaveChangesAsync();
    }
}
