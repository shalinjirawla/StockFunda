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

namespace StockLens_BusinessLayer.Services
{
    public class StockPriceHistoryService : IStockPriceHistoryService
    {
        private readonly IStockPriceHistoryRepository _priceHistoryRepository;
        private readonly IStockRepository _stockRepository;
        private readonly ICompanyRepository _companyRepository;
        private readonly IIndianApiHistoricalDataClient _apiClient;
        private readonly ILogger<StockPriceHistoryService> _logger;

        public StockPriceHistoryService(
            IStockPriceHistoryRepository priceHistoryRepository,
            IStockRepository stockRepository,
            ICompanyRepository companyRepository,
            IIndianApiHistoricalDataClient apiClient,
            ILogger<StockPriceHistoryService> logger)
        {
            _priceHistoryRepository = priceHistoryRepository;
            _stockRepository = stockRepository;
            _companyRepository = companyRepository;
            _apiClient = apiClient;
            _logger = logger;
        }

        public async Task<PriceHistoryResponseDto> GetPriceHistoryBySymbolAsync(string symbol, string? exchange = null, string period = "5yr", bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return new PriceHistoryResponseDto { ErrorMessage = "Symbol is required." };
            }

            var cleanSymbol = symbol.Trim().ToUpperInvariant();
            var cleanExchange = string.IsNullOrWhiteSpace(exchange) ? "NSE" : exchange.Trim().ToUpperInvariant();

            var stock = await _stockRepository.GetBySymbolAsync(cleanSymbol, cleanExchange);

            if (stock == null)
            {
                _logger.LogInformation("Stock {Symbol} ({Exchange}) not found in DB. Auto-registering stock.", cleanSymbol, cleanExchange);
                var existingCompany = await _companyRepository.GetCompanyBySymbolAsync(cleanSymbol);
                stock = new Stock
                {
                    Symbol = cleanSymbol,
                    Exchange = cleanExchange,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                if (existingCompany != null)
                {
                    stock.CompanyId = existingCompany.Id;
                    stock.Company = existingCompany;
                }
                else
                {
                    stock.Company = new Company
                    {
                        CompanyName = $"{cleanSymbol} Limited",
                        Symbol = cleanSymbol,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                }

                stock = await _stockRepository.AddAsync(stock);
                await _stockRepository.SaveChangesAsync();
            }

            return await ProcessPriceHistoryAsync(stock, period, forceRefresh, cancellationToken);
        }

        public async Task<PriceHistoryResponseDto> GetPriceHistoryByStockIdAsync(int stockId, string period = "5yr", bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            var stock = await _stockRepository.GetByIdAsync(stockId);
            if (stock == null)
            {
                return new PriceHistoryResponseDto { ErrorMessage = "Stock not found." };
            }

            return await ProcessPriceHistoryAsync(stock, period, forceRefresh, cancellationToken);
        }

        private void ParsePeriod(string period, out DateTime fromDate, out int expectedDays)
        {
            var p = (period ?? "5yr").ToLowerInvariant();
            var now = DateTime.UtcNow;
            
            switch (p)
            {
                case "1m":
                    fromDate = now.AddMonths(-1);
                    expectedDays = 20;
                    break;
                case "6m":
                    fromDate = now.AddMonths(-6);
                    expectedDays = 125;
                    break;
                case "1yr":
                    fromDate = now.AddYears(-1);
                    expectedDays = 250;
                    break;
                case "3yr":
                    fromDate = now.AddYears(-3);
                    expectedDays = 750;
                    break;
                case "5yr":
                    fromDate = now.AddYears(-5);
                    expectedDays = 1250;
                    break;
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

        private async Task<PriceHistoryResponseDto> ProcessPriceHistoryAsync(Stock stock, string period, bool forceRefresh, CancellationToken cancellationToken)
        {
            var result = new PriceHistoryResponseDto { Symbol = stock.Symbol };

            try
            {
                var dbRecords = await _priceHistoryRepository.GetByStockIdAsync(stock.Id, cancellationToken);

                ParsePeriod(period, out var expectedFromDate, out var expectedDays);

                bool needsRefresh = forceRefresh;
                if (!needsRefresh && dbRecords.Any())
                {
                    // Check if data is stale (e.g. last sync was more than 12 hours ago)
                    var lastSync = dbRecords.Max(p => p.LastSyncedAt);
                    if ((DateTime.UtcNow - lastSync).TotalHours > 12)
                    {
                        needsRefresh = true;
                    }
                    // Force refresh if we don't have enough data for the requested period
                    // (and we have at least *some* data older than the expected from date)
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
                    _logger.LogInformation("Price history data is missing or stale. Fetching from API for {Symbol} with period 5yr (to cache maximum daily resolution)", stock.Symbol);
                    
                    var rawPrices = await _apiClient.GetHistoricalPricesAsync(stock.Symbol, period: "5yr", exchange: stock.Exchange, cancellationToken: cancellationToken);
                    
                    if (rawPrices != null && rawPrices.Count > 0)
                    {
                        // Filter out records without valid dates
                        var validRecords = rawPrices.Where(r => r.ResolvedDate.HasValue).ToList();
                        
                        if (validRecords.Any())
                        {
                            await _priceHistoryRepository.RemoveRangeAsync(dbRecords, cancellationToken);
                            
                            var newRecords = validRecords.Select(r => new StockPriceHistory
                            {
                                StockId = stock.Id,
                                Date = r.ResolvedDate!.Value,
                                Open = r.Open ?? 0,
                                High = r.High ?? 0,
                                Low = r.Low ?? 0,
                                Close = r.Close ?? 0,
                                Volume = r.Volume ?? 0,
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
                result.OpenPrices.Add(record.Open);
                result.HighPrices.Add(record.High);
                result.LowPrices.Add(record.Low);
                result.ClosePrices.Add(record.Close);
                result.Volumes.Add(record.Volume);
            }

            var latestRecord = sortedDbRecords.LastOrDefault();
            if (latestRecord != null)
            {
                result.LastSyncedAt = latestRecord.LastSyncedAt.ToString("O");
                result.Source = latestRecord.Source;
            }

            return result;
        }
    }
}
