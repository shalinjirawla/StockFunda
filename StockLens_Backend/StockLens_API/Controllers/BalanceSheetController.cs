using Microsoft.AspNetCore.Mvc;
using StockLens_BusinessLayer.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_API.Controllers
{
    [ApiController]
    [Route("api/stocks")]
    [Produces("application/json")]
    public class BalanceSheetController : ControllerBase
    {
        private readonly IStockBalanceSheetService _balanceSheetService;
        public BalanceSheetController(
            IStockBalanceSheetService balanceSheetService,
            StockLens_Infrastructure.ExternalServices.BharatStock.IFinancialProvider bharatStockApi)
        {
            _balanceSheetService = balanceSheetService;
        }

        [HttpGet("balancesheet")]
        public async Task<IActionResult> GetBalanceSheet(
            [FromQuery] string symbol, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return BadRequest(new { Message = "The 'symbol' query parameter is required." });
            }

            var result = await _balanceSheetService.GetBalanceSheetAsync(symbol, cancellationToken);

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }

            return Ok(result);

        }
    }
}
