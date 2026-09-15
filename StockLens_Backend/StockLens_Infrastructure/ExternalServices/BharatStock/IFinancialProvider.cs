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

        Task<BharatStockRatiosRecord?> GetRatiosAsync(
            string ticker,
            CancellationToken cancellationToken = default);

        Task<BharatStockCompanyDetailsRecord?> GetStockDetailsAsync(
            string ticker,
            string? exchange = "NSE",
            CancellationToken cancellationToken = default);

        Task<BharatStockScreenerRecord?> GetScreenerDataAsync(
            string ticker,
            string? exchange = "NSE",
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BharatStockScreenerRecord>> GetScreenerBySectorAsync(
            string sector,
            string? exchange = "NSE",
            int page = 1,
            int pageSize = 200,
            CancellationToken cancellationToken = default);
    }
}

