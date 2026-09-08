using Microsoft.EntityFrameworkCore;
using StockLens_DataLayer.Entities;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.DataContext;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.Repositories
{
    public class StockRepository : IStockRepository
    {
        private readonly StockLensDataContext _context;

        public StockRepository(StockLensDataContext context)
        {
            _context = context;
        }

        public async Task<Stock?> GetByIdAsync(int id)
        {
            return await _context.Stocks
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Stock?> GetBySymbolAsync(string symbol, string? exchange = null)
        {
            var query = _context.Stocks.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(exchange))
            {
                return await query.FirstOrDefaultAsync(s => s.Symbol.ToLower() == symbol.ToLower() && s.Exchange.ToLower() == exchange.ToLower());
            }

            return await query.FirstOrDefaultAsync(s => s.Symbol.ToLower() == symbol.ToLower());
        }

        public async Task<IEnumerable<Stock>> GetAllStocksAsync()
        {
            return await _context.Stocks
                .AsNoTracking()
                .OrderBy(s => s.Symbol)
                .ToListAsync();
        }

        public async Task<Stock> AddAsync(Stock stock)
        {
            var entry = await _context.Stocks.AddAsync(stock);
            return entry.Entity;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
