using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StockLens_BusinessLayer.DTOs;
using StockLens_BusinessLayer.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_API.Controllers
{
    [ApiController]
    [Route("api/stocks")]
    [Produces("application/json")]
    public class StockNewsController : ControllerBase
    {
        private readonly IStockNewsService _stockNewsService;

        public StockNewsController(IStockNewsService stockNewsService)
        {
            _stockNewsService = stockNewsService;
        }

        /// <summary>
        /// Retrieves the list of available stocks.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<StockDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStocks()
        {
            var stocks = await _stockNewsService.GetAllStocksAsync();
            return Ok(stocks);
        }

        /// <summary>
        /// Retrieves latest news for a specific stock by its Stock ID.
        /// </summary>
        /// <param name="stockId">The database ID of the stock.</param>
        /// <param name="limit">Max number of news items to return (default: 20, max: 100).</param>
        /// <param name="page">Page index (default: 1).</param>
        /// <param name="refresh">Force cache refresh from IndianAPI.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{stockId:int}/news")]
        [ProducesResponseType(typeof(StockNewsResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNewsByStockId(
            int stockId,
            [FromQuery] int limit = 20,
            [FromQuery] int page = 1,
            [FromQuery] bool refresh = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _stockNewsService.GetLatestNewsByStockIdAsync(stockId, limit, page, refresh, cancellationToken);
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while fetching stock news.", detail = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves latest news for a stock by symbol and exchange (NSE/BSE).
        /// </summary>
        /// <param name="symbol">Stock ticker symbol (e.g., RELIANCE, TCS, INFY).</param>
        /// <param name="exchange">Stock exchange (NSE or BSE, default: NSE).</param>
        /// <param name="limit">Max number of news items to return.</param>
        /// <param name="page">Page index.</param>
        /// <param name="refresh">Force cache refresh from IndianAPI.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("news")]
        [ProducesResponseType(typeof(StockNewsResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNewsBySymbol(
            [FromQuery] string symbol,
            [FromQuery] string? exchange = "NSE",
            [FromQuery] int limit = 20,
            [FromQuery] int page = 1,
            [FromQuery] bool refresh = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return BadRequest(new { message = "The 'symbol' parameter is required." });
            }

            try
            {
                var response = await _stockNewsService.GetLatestNewsBySymbolAsync(symbol, exchange, limit, page, refresh, cancellationToken);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while fetching stock news.", detail = ex.Message });
            }
        }
    }
}
