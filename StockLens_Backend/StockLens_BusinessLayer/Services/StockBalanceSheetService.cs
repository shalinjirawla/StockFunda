using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockLens_BusinessLayer.Models;
using StockLens_BusinessLayer.Interfaces;
using StockLens_DataLayer.Entities;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.ExternalServices.IndianApi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Services
{
    public class StockBalanceSheetService : IStockBalanceSheetService
    {
        private readonly IIndianApiBalanceSheetClient _apiClient;
        private readonly IStockRepository _stockRepository;
        private readonly IStockBalanceSheetRepository _balanceSheetRepository;
        private readonly ICompanyRepository _companyRepository;
        private readonly ILogger<StockBalanceSheetService> _logger;

        public StockBalanceSheetService(
            IIndianApiBalanceSheetClient apiClient, 
            IStockRepository stockRepository,
            IStockBalanceSheetRepository balanceSheetRepository,
            ICompanyRepository companyRepository,
            ILogger<StockBalanceSheetService> logger)
        {
            _apiClient = apiClient;
            _stockRepository = stockRepository;
            _balanceSheetRepository = balanceSheetRepository;
            _companyRepository = companyRepository;
            _logger = logger;
        }

        public async Task<BalanceSheetResponseDto> GetBalanceSheetAsync(
            string symbol,
            string? exchange = "NSE",
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
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

                return await ProcessBalanceSheetAsync(stock, forceRefresh, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing balance sheet for {Symbol}", symbol);
                return new BalanceSheetResponseDto 
                { 
                    Symbol = symbol,
                    ErrorMessage = "An error occurred while fetching balance sheet data." 
                };
            }
        }

        public async Task<BalanceSheetResponseDto> GetBalanceSheetByStockIdAsync(
            int stockId,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var stock = await _stockRepository.GetByIdAsync(stockId);
                if (stock == null)
                {
                    throw new KeyNotFoundException($"Stock with ID {stockId} was not found.");
                }

                return await ProcessBalanceSheetAsync(stock, forceRefresh, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing balance sheet for Stock ID {StockId}", stockId);
                return new BalanceSheetResponseDto 
                { 
                    Symbol = "Unknown",
                    ErrorMessage = "An error occurred while fetching balance sheet data." 
                };
            }
        }

        private async Task<BalanceSheetResponseDto> ProcessBalanceSheetAsync(Stock stock, bool forceRefresh, CancellationToken cancellationToken)
        {
            var result = new BalanceSheetResponseDto { Symbol = stock.Symbol };

            try
            {
                // Check DB for recent records
                var dbRecords = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 3);

                bool needsRefresh = forceRefresh;
                if (!needsRefresh && dbRecords.Any())
                {
                    // Check if data is fresh (synced within last 7 days)
                    var lastSync = dbRecords.Max(b => b.LastSyncedAt);
                    if ((DateTime.UtcNow - lastSync).TotalDays > 7)
                    {
                        needsRefresh = true;
                    }
                }
                else if (!dbRecords.Any())
                {
                    needsRefresh = true;
                }

                if (needsRefresh)
                {
                    _logger.LogInformation("Balance sheet data is missing or stale. Fetching from IndianAPI for {Symbol}", stock.Symbol);
                    var rawBalanceSheet = await _apiClient.GetBalanceSheetAsync(stock.Symbol, cancellationToken);
                    
                    if (rawBalanceSheet != null && rawBalanceSheet.Count > 0)
                    {
                        var periods = ExtractPeriods(rawBalanceSheet);
                        // Take only the last 3 periods
                        var last3Periods = periods.OrderByDescending(p => p.ParsedDate).Take(3).ToList();
                        
                        // Delete existing records for this stock before inserting new ones
                        await _balanceSheetRepository.RemoveRangeAsync(dbRecords);
                        
                        var newRecords = new List<StockBalanceSheet>();
                        
                        foreach (var period in last3Periods)
                        {
                            var record = new StockBalanceSheet
                            {
                                StockId = stock.Id,
                                PeriodKey = $"annual-{period.ParsedDate:yyyy-MM-dd}",
                                PeriodType = "annual",
                                FiscalYear = period.PeriodStr,
                                PeriodEndDate = period.ParsedDate,
                                ConsolidationType = "consolidated", // Default for now
                                Source = "IndianAPI",
                                LastSyncedAt = DateTime.UtcNow,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                
                                EquityCapital = GetValue(rawBalanceSheet, "Equity Capital", period.PeriodStr),
                                Reserves = GetValue(rawBalanceSheet, "Reserves", period.PeriodStr),
                                Borrowings = GetValue(rawBalanceSheet, "Borrowings", period.PeriodStr),
                                OtherLiabilities = GetValue(rawBalanceSheet, "Other Liabilities", period.PeriodStr),
                                TotalLiabilities = GetValue(rawBalanceSheet, "Total Liabilities", period.PeriodStr),
                                FixedAssets = GetValue(rawBalanceSheet, "Fixed Assets", period.PeriodStr),
                                Cwip = GetValue(rawBalanceSheet, "CWIP", period.PeriodStr),
                                Investments = GetValue(rawBalanceSheet, "Investments", period.PeriodStr),
                                OtherAssets = GetValue(rawBalanceSheet, "Other Assets", period.PeriodStr),
                                TotalAssets = GetValue(rawBalanceSheet, "Total Assets", period.PeriodStr)
                            };
                            newRecords.Add(record);
                        }

                        await _balanceSheetRepository.AddRangeAsync(newRecords);
                        await _balanceSheetRepository.SaveChangesAsync();
                        
                        // Fetch fresh data from DB after insertion
                        dbRecords = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 3);
                    }
                    else if (!dbRecords.Any())
                    {
                        result.ErrorMessage = $"Data unavailable for {stock.Symbol}. Could not fetch from IndianAPI.";
                        return result;
                    }
                }

                return MapToResponseDto(stock, dbRecords.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessBalanceSheetAsync for {Symbol}", stock.Symbol);
                result.ErrorMessage = "An error occurred while processing balance sheet data.";
                return result;
            }
        }

        private BalanceSheetResponseDto MapToResponseDto(Stock stock, List<StockBalanceSheet> dbRecords)
        {
            var result = new BalanceSheetResponseDto { Symbol = stock.Symbol };

            // Construct Response DTO from dbRecords (which now only contains max 3 records)
            var sortedDbRecords = dbRecords.OrderBy(b => b.PeriodEndDate).ToList(); // Sort chronologically for UI
            result.Periods = sortedDbRecords.Select(b => b.FiscalYear).ToList();

            result.LineItems = new List<BalanceSheetLineItemDto>
            {
                new() { Name = "Fixed Assets", Values = sortedDbRecords.Select(b => b.FixedAssets).ToList() },
                new() { Name = "CWIP", Values = sortedDbRecords.Select(b => b.Cwip).ToList() },
                new() { Name = "Investments", Values = sortedDbRecords.Select(b => b.Investments).ToList() },
                new() { Name = "Other Assets", Values = sortedDbRecords.Select(b => b.OtherAssets).ToList() },
                new() { Name = "Total Assets", IsTotal = true, Values = sortedDbRecords.Select(b => b.TotalAssets).ToList() }
            };

            // Add Metadata
            var latestRecord = sortedDbRecords.LastOrDefault();
            if (latestRecord != null)
            {
                result.ConsolidationType = latestRecord.ConsolidationType?.ToUpperInvariant() ?? "CONSOLIDATED";
                result.LatestPeriodEnd = latestRecord.PeriodEndDate?.ToString("dd MMM yyyy") ?? "";
                result.Source = latestRecord.Source ?? "IndianAPI";
                result.LastSyncedAt = latestRecord.LastSyncedAt.ToString("O");
            }

            // Calculate YoY Growth Percentage for Total Assets
            if (sortedDbRecords.Count >= 2)
            {
                // Assuming sortedDbRecords is sorted oldest to newest (by PeriodEndDate ascending)
                var latest = sortedDbRecords.Last().TotalAssets;
                var previous = sortedDbRecords[^2].TotalAssets;
                
                if (latest != null && previous != null && previous != 0)
                {
                    result.AssetGrowthPercentage = ((latest - previous) / Math.Abs(previous.Value)) * 100;
                }
            }

            return result;
        }

        private List<(string PeriodStr, DateTime ParsedDate)> ExtractPeriods(Dictionary<string, Dictionary<string, decimal?>> rawBalanceSheet)
        {
            var periods = new List<(string PeriodStr, DateTime ParsedDate)>();
            
            // Assume the first key's inner dictionary has all periods
            var firstKey = rawBalanceSheet.Keys.FirstOrDefault();
            if (string.IsNullOrEmpty(firstKey) || !rawBalanceSheet.TryGetValue(firstKey, out var rowData))
            {
                return periods;
            }

            foreach (var key in rowData.Keys)
            {
                if (DateTime.TryParseExact(key, "MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    periods.Add((key, parsedDate));
                }
            }

            return periods;
        }

        private decimal? GetValue(Dictionary<string, Dictionary<string, decimal?>> rawBalanceSheet, string lineItemName, string period)
        {
            // Case-insensitive match for the line item key
            var key = rawBalanceSheet.Keys.FirstOrDefault(k => k.Equals(lineItemName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(key) && rawBalanceSheet.TryGetValue(key, out var rowData))
            {
                if (rowData.TryGetValue(period, out var val))
                {
                    return val;
                }
            }
            return null;
        }
    }
}
