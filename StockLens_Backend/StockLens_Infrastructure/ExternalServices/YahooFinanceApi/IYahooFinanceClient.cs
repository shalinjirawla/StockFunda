using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.YahooFinanceApi
{
    public interface IYahooFinanceClient
    {
        /// <summary>
        /// Fetches the real company name (shortname or longname) and industry from Yahoo Finance API.
        /// </summary>
        /// <param name="symbol">The stock symbol (e.g. INFY)</param>
        /// <param name="exchange">The exchange (e.g. NSE or BSE)</param>
        /// <returns>A tuple containing the company name and industry</returns>
        Task<(string? CompanyName, string? Industry)> GetCompanyDetailsAsync(string symbol, string exchange, CancellationToken cancellationToken = default);
    }
}
