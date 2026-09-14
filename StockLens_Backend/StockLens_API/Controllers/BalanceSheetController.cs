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

        [HttpGet("{stockId:int}/balancesheet")]
        public async Task<IActionResult> GetBalanceSheetByStockId(
            int stockId,
            [FromQuery] bool refresh = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _balanceSheetService.GetBalanceSheetByStockIdAsync(stockId, refresh, cancellationToken);
                
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    return BadRequest(new { Message = result.ErrorMessage });
                }

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }

        [HttpGet("balancesheet")]
        public async Task<IActionResult> GetBalanceSheet(
            [FromQuery] string symbol, 
            [FromQuery] string? exchange = "NSE",
            [FromQuery] bool refresh = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return BadRequest(new { Message = "The 'symbol' query parameter is required." });
            }

            var result = await _balanceSheetService.GetBalanceSheetAsync(symbol, exchange, refresh, cancellationToken);

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }

            return Ok(result);
        }
    }
}
