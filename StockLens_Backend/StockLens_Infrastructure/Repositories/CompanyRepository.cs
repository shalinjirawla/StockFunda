using Microsoft.EntityFrameworkCore;
using StockLens_DataLayer.Entities;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.DataContext;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.Repositories
{
    public class CompanyRepository : ICompanyRepository
    {
        private readonly StockLensDataContext _context;

        public CompanyRepository(StockLensDataContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Company>> SearchCompaniesAsync(string query, int limit)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<Company>();

            var lowerQuery = query.ToLower();
            
            return await _context.CompanyMaster
                .AsNoTracking()
                .Where(c => c.Symbol.ToLower().Contains(lowerQuery) || c.CompanyName.ToLower().Contains(lowerQuery))
                .OrderBy(c => c.Symbol.ToLower().StartsWith(lowerQuery) ? 0 : 1)
                .ThenBy(c => c.Symbol)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<Company?> GetCompanyBySymbolAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return null;

            return await _context.CompanyMaster
                .FirstOrDefaultAsync(c => c.Symbol.ToLower() == symbol.ToLower());
        }
    }
}
