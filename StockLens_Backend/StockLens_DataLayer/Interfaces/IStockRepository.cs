using StockLens_DataLayer.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockLens_DataLayer.Interfaces
{
    public interface IStockRepository
    {
        Task<Stock?> GetByIdAsync(int id);
        Task<Stock?> GetBySymbolAsync(string symbol, string? exchange = null);
        Task<IEnumerable<Stock>> GetAllStocksAsync();
        Task<Stock> AddAsync(Stock stock);
        Task<int> SaveChangesAsync();
    }
}
