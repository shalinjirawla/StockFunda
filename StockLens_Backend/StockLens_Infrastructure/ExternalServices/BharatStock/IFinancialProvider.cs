using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.BharatStock
{
    public interface IFinancialProvider
    {
        Task<IReadOnlyList<BharatStockFinancialRecord>> GetFinancialsAsync(
            string ticker,
            string periodType = "annual",
            int page = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default);
    }
}
