using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StockLens_BusinessLayer.DTOs;
using StockLens_BusinessLayer.Interfaces;
using StockLens_DataLayer.Entities;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.ExternalServices.IndianApi;
using StockLens_Infrastructure.ExternalServices.IndianApi.Models;

using StockLens_Infrastructure.ExternalServices.YahooFinanceApi;

namespace StockLens_BusinessLayer.Services
{
    public class StockPriceHistoryService : IStockPriceHistoryService
    {
        private readonly IStockPriceHistoryRepository _priceHistoryRepository;
        private readonly IStockRepository _stockRepository;
        private readonly ICompanyRepository _companyRepository;
        private readonly IIndianApiHistoricalDataClient _apiClient;
        private readonly IYahooFinanceClient _yahooFinanceClient;
        private readonly ILogger<StockPriceHistoryService> _logger;

        public StockPriceHistoryService(
            IStockPriceHistoryRepository priceHistoryRepository,
            IStockRepository stockRepository,
            ICompanyRepository companyRepository,
            IIndianApiHistoricalDataClient apiClient,
            IYahooFinanceClient yahooFinanceClient,
            ILogger<StockPriceHistoryService> logger)
        {
            _priceHistoryRepository = priceHistoryRepository;
            _stockRepository = stockRepository;
            _companyRepository = companyRepository;
            _apiClient = apiClient;
            _yahooFinanceClient = yahooFinanceClient;
            _logger = logger;
        }

        public async Task<PriceHistoryResponseDto> GetPriceHistoryBySymbolAsync(string symbol, string? exchange = null, string period = "5yr", bool forceRefresh = false, string filter = "price", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return new PriceHistoryResponseDto { ErrorMessage = "Symbol is required." };
            }

            var cleanSymbol = symbol.Trim().ToUpperInvariant();
            var cleanExchange = string.IsNullOrWhiteSpace(exchange) ? "NSE" : exchange.Trim().ToUpperInvariant();

            var stock = await _stockRepository.GetOrCreateStockAsync(cleanSymbol, cleanExchange, cancellationToken: cancellationToken);

            return await ProcessPriceHistoryAsync(stock, period, forceRefresh, filter, cancellationToken);
        }

        public async Task<PriceHistoryResponseDto> GetPriceHistoryByStockIdAsync(int stockId, string period = "5yr", bool forceRefresh = false, string filter = "price", CancellationToken cancellationToken = default)
        {
            var stock = await _stockRepository.GetByIdAsync(stockId);
            if (stock == null)
            {
                return new PriceHistoryResponseDto { ErrorMessage = "Stock not found." };
            }

            return await ProcessPriceHistoryAsync(stock, period, forceRefresh, filter, cancellationToken);
        }

        private void ParsePeriod(string period, out DateTime fromDate, out int expectedDays)
        {
            var p = (period ?? "5yr").ToLowerInvariant().Trim();
            var now = DateTime.UtcNow;
            
            switch (p)
            {
                case "1m":
                    fromDate = now.AddMonths(-1);
                    expectedDays = 20;
                    break;
                case "3m":
                    fromDate = now.AddMonths(-3);
                    expectedDays = 60;
                    break;
                case "6m":
                    fromDate = now.AddMonths(-6);
                    expectedDays = 125;
                    break;
                case "1y":
                case "1yr":
                    fromDate = now.AddYears(-1);
                    expectedDays = 250;
                    break;
                case "3y":
                case "3yr":
                    fromDate = now.AddYears(-3);
                    expectedDays = 750;
                    break;
                case "5y":
                case "5yr":
                    fromDate = now.AddYears(-5);
                    expectedDays = 1250;
                    break;
                case "10y":
                case "10yr":
                    fromDate = now.AddYears(-10);
                    expectedDays = 2500;
                    break;
                case "max":
                    fromDate = now.AddYears(-30);
                    expectedDays = 7500;
                    break;
                default:
                    fromDate = now.AddYears(-5);
                    expectedDays = 1250;
                    break;
            }
        }

        private async Task<PriceHistoryResponseDto> ProcessPriceHistoryAsync(Stock stock, string period, bool forceRefresh, string filter, CancellationToken cancellationToken)
        {
            var result = new PriceHistoryResponseDto { Symbol = stock.Symbol };

            try
            {
                var dbRecords = await _priceHistoryRepository.GetByStockIdAsync(stock.Id, cancellationToken);

                ParsePeriod(period, out var expectedFromDate, out var expectedDays);

                bool needsRefresh = forceRefresh;
                if (!needsRefresh && dbRecords.Any())
                {
                    // If cached records lack authentic OHLC data (e.g. Open/High/Low were zeroed or identical to Close from older IndianAPI caching), refresh
                    var hasValidOhlc = dbRecords.Any(p => p.High > p.Low && p.Open > 0);
                    var lastSync = dbRecords.Max(p => p.LastSyncedAt);

                    if (!hasValidOhlc && dbRecords.Count > 10)
                    {
                        _logger.LogInformation("Cached price history for {Symbol} lacks OHLC variance. Forcing refresh from Yahoo Finance.", stock.Symbol);
                        needsRefresh = true;
                    }
                    // Check if data is stale (e.g. last sync was more than 12 hours ago)
                    else if ((DateTime.UtcNow - lastSync).TotalHours > 12)
                    {
                        needsRefresh = true;
                    }
                    // Force refresh if we don't have enough data for the requested period
                    else 
                    {
                        var recordsInPeriod = dbRecords.Count(p => p.Date >= expectedFromDate);
                        if (recordsInPeriod < expectedDays * 0.8 && dbRecords.Min(p => p.Date) > expectedFromDate)
                        {
                            _logger.LogInformation("Cached data for {Symbol} doesn't cover requested period {Period}. Forcing refresh.", stock.Symbol, period);
                            needsRefresh = true;
                        }
                    }
                }
                else if (!dbRecords.Any())
                {
                    needsRefresh = true;
                }

                if (needsRefresh)
                {
                    _logger.LogInformation("Price history data is missing, stale, or needs OHLC. Fetching from Yahoo Finance API for {Symbol}", stock.Symbol);
                    
                    List<IndianApiPriceRecord>? rawPrices = null;
                    var source = "YahooFinance";

                    try
                    {
                        rawPrices = await _yahooFinanceClient.GetHistoricalPricesAsync(stock.Symbol, stock.Exchange, cancellationToken);
                        if (rawPrices != null && rawPrices.Count > 0)
                        {
                            source = "YahooFinance";
                        }
                    }
                    catch (Exception yfEx)
                    {
                        _logger.LogWarning(yfEx, "Failed to fetch OHLC from Yahoo Finance for {Symbol}. Trying IndianAPI fallback.", stock.Symbol);
                    }

                    if (rawPrices == null || rawPrices.Count == 0)
                    {
                        _logger.LogInformation("Yahoo Finance returned no prices for {Symbol}. Attempting fallback to IndianAPI.", stock.Symbol);
                        try
                        {
                            rawPrices = await _apiClient.GetHistoricalPricesAsync(stock.Symbol, period: "5yr", exchange: stock.Exchange, filter: filter, cancellationToken: cancellationToken);
                            if (rawPrices != null && rawPrices.Count > 0)
                            {
                                source = "IndianAPI";
                            }
                        }
                        catch (Exception apiEx)
                        {
                            _logger.LogWarning(apiEx, "Failed to fetch from IndianAPI fallback for {Symbol}", stock.Symbol);
                        }
                    }

                    if (rawPrices != null && rawPrices.Count > 0)
                    {
                        // Filter out records without valid dates
                        var validRecords = rawPrices.Where(r => r.ResolvedDate.HasValue).OrderBy(r => r.ResolvedDate!.Value).ToList();
                        
                        if (validRecords.Any())
                        {
                            // Calculate 50 DMA and 200 DMA if missing
                            for (int i = 0; i < validRecords.Count; i++)
                            {
                                var r = validRecords[i];
                                if (!r.Dma50.HasValue && i >= 49)
                                {
                                    r.Dma50 = Math.Round(validRecords.Skip(i - 49).Take(50).Average(x => x.Close ?? 0), 2);
                                }
                                if (!r.Dma200.HasValue && i >= 199)
                                {
                                    r.Dma200 = Math.Round(validRecords.Skip(i - 199).Take(200).Average(x => x.Close ?? 0), 2);
                                }
                            }

                            await _priceHistoryRepository.RemoveRangeAsync(dbRecords, cancellationToken);
                            
                            var newRecords = validRecords.Select(r => new StockPriceHistory
                            {
                                StockId = stock.Id,
                                Date = r.ResolvedDate!.Value,
                                Open = r.Open ?? (r.Close ?? 0),
                                High = r.High ?? (r.Close ?? 0),
                                Low = r.Low ?? (r.Close ?? 0),
                                Close = r.Close ?? 0,
                                Volume = r.Volume ?? 0,
                                Dma50 = r.Dma50,
                                Dma200 = r.Dma200,
                                Source = source,
                                LastSyncedAt = DateTime.UtcNow,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            }).ToList();

                            await _priceHistoryRepository.AddRangeAsync(newRecords, cancellationToken);
                            await _priceHistoryRepository.SaveChangesAsync(cancellationToken);
                            
                            dbRecords = newRecords;
                        }
                    }
                    else if (!dbRecords.Any())
                    {
                        result.ErrorMessage = $"Price history unavailable for {stock.Symbol}.";
                        return result;
                    }
                }

                // Filter to return only the requested period
                var filteredRecords = dbRecords.Where(r => r.Date >= expectedFromDate).ToList();
                return MapToResponseDto(stock, filteredRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessPriceHistoryAsync for {Symbol}", stock.Symbol);
                result.ErrorMessage = "An error occurred while processing price history data.";
                return result;
            }
        }

        private PriceHistoryResponseDto MapToResponseDto(Stock stock, List<StockPriceHistory> dbRecords)
        {
            var result = new PriceHistoryResponseDto { Symbol = stock.Symbol };

            var sortedDbRecords = dbRecords.OrderBy(p => p.Date).ToList();

            foreach (var record in sortedDbRecords)
            {
                result.Dates.Add(record.Date.ToString("yyyy-MM-dd"));
                result.Opens.Add(record.Open > 0 ? record.Open : record.Close);
                result.Highs.Add(record.High > 0 ? record.High : Math.Max(record.Open, record.Close));
                result.Lows.Add(record.Low > 0 ? record.Low : Math.Min(record.Open, record.Close));
                result.ClosePrices.Add(record.Close);
                result.Volumes.Add(record.Volume);
                result.Dma50.Add(record.Dma50);
                result.Dma200.Add(record.Dma200);
            }

            var latestRecord = sortedDbRecords.LastOrDefault();
            if (latestRecord != null)
            {
                var dt = latestRecord.LastSyncedAt;
                if (dt.Kind == DateTimeKind.Unspecified)
                {
                    dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
                result.LastSyncedAt = dt.ToString("O");
                result.Source = latestRecord.Source;
            }

            return result;
        }
    }
}
