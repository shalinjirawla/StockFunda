using StockLens_BusinessLayer.Models;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Interfaces
{
    public interface IStockBalanceSheetService
    {
        Task<BalanceSheetResponseDto> GetBalanceSheetAsync(string symbol, CancellationToken cancellationToken = default);
    }
}
