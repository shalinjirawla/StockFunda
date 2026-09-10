using StockLens_DataLayer.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockLens_DataLayer.Interfaces
{
    public interface IStockFinancialRepository
    {
        Task<List<StockFinancial>> GetFinancialsByStockIdAsync(int stockId, string periodType = "annual", int limit = 3);
        Task<StockFinancial?> GetByStockIdAndPeriodKeyAsync(int stockId, string periodKey);
        Task<StockFinancial> AddAsync(StockFinancial financial);
        Task UpdateAsync(StockFinancial financial);
        Task<int> SaveChangesAsync();
    }
}
