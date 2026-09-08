using StockLens_DataLayer.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockLens_DataLayer.Interfaces
{
    public interface IStockNewsRepository
    {
        Task<IEnumerable<StockNews>> GetNewsByStockIdAsync(int stockId, int limit = 20, int page = 1);
        Task<DateTime?> GetLatestFetchedAtByStockIdAsync(int stockId);
        Task<HashSet<string>> GetExistingExternalNewsIdsAsync(int stockId, IEnumerable<string> externalNewsIds);
        Task<HashSet<string>> GetExistingSourceUrlsAsync(int stockId, IEnumerable<string> sourceUrls);
        Task AddRangeAsync(IEnumerable<StockNews> newsItems);
        Task<int> SaveChangesAsync();
    }
}
