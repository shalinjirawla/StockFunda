using StockLens_Infrastructure.ExternalServices.IndianApi.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.IndianApi
{
    public interface IIndianApiShareholdingClient
    {
        Task<IReadOnlyList<IndianApiNormalizedQuarterRecord>> GetQuarterlyShareholdingAsync(
            string stockName,
            CancellationToken cancellationToken = default);
    }
}
