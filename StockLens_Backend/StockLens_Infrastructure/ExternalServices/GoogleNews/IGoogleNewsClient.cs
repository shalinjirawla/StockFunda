using StockLens_Infrastructure.ExternalServices.IndianApi.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.GoogleNews
{
    public interface IGoogleNewsClient
    {
        Task<IReadOnlyList<IndianApiStandardArticle>> GetStockNewsAsync(
            string symbol,
            string? companyName = null,
            CancellationToken cancellationToken = default);
    }
}
