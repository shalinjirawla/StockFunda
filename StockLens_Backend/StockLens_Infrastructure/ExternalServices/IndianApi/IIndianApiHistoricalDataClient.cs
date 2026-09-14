using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StockLens_Infrastructure.ExternalServices.IndianApi.Models;

namespace StockLens_Infrastructure.ExternalServices.IndianApi
{
    public interface IIndianApiHistoricalDataClient
    {
        Task<List<IndianApiPriceRecord>> GetHistoricalPricesAsync(string ticker, string? from, string? to, string? exchange = null, CancellationToken cancellationToken = default);
    }
}
