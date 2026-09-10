using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.BharatStock
{
    public interface IShareholdingProvider
    {
        Task<IReadOnlyList<BharatStockShareholdingRecord>> GetShareholdingAsync(
            string ticker,
            CancellationToken cancellationToken = default);
    }
}
