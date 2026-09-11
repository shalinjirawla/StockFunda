using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.IndianApi
{
    public interface IIndianApiFinancialsClient
    {
        Task<Dictionary<string, Dictionary<string, decimal?>>?> GetBalanceSheetAsync(string symbol, CancellationToken cancellationToken = default);
    }
}
