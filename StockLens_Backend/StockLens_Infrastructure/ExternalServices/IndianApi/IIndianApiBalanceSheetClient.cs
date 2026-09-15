using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StockLens_Infrastructure.ExternalServices.YahooFinanceApi;

namespace StockLens_Infrastructure.ExternalServices.IndianApi
{
    public interface IIndianApiBalanceSheetClient
    {
        Task<Dictionary<string, Dictionary<string, decimal?>>?> GetBalanceSheetAsync(string symbol, CancellationToken cancellationToken = default);
        Task<decimal?> GetCurrentPriceAsync(string symbol, string? exchange = "NSE", CancellationToken cancellationToken = default);
        Task<YahooLiveQuoteDto?> GetLiveQuoteAsync(string symbol, string? exchange = "NSE", CancellationToken cancellationToken = default);
        Task<IndianApiStockOverviewDto?> GetStockFinancialsAndOverviewAsync(string symbol, string? exchange = "NSE", CancellationToken cancellationToken = default);
    }
}

