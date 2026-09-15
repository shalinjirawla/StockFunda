using AutoMapper;
using Microsoft.Extensions.Logging;
using StockLens_BusinessLayer.DTOs;
using StockLens_BusinessLayer.Interfaces;
using StockLens_DataLayer.Entities;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.ExternalServices.IndianApi;
using StockLens_Infrastructure.ExternalServices.YahooFinanceApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Services
{
    public class StockQuarterlyResultsService : IStockQuarterlyResultsService
    {
        private readonly IStockRepository _stockRepository;
        private readonly IStockFinancialRepository _financialRepository;
        private readonly ICompanyRepository _companyRepository;
        private readonly IIndianApiBalanceSheetClient? _indianApiClient;
        private readonly IYahooFinanceClient? _yahooFinanceClient;
        private readonly IMapper _mapper;
        private readonly ILogger<StockQuarterlyResultsService> _logger;

        private static readonly ConcurrentDictionary<int, SemaphoreSlim> StockLocks = new();

        public StockQuarterlyResultsService(
            IStockRepository stockRepository,
            IStockFinancialRepository financialRepository,
            ICompanyRepository companyRepository,
            IMapper mapper,
            ILogger<StockQuarterlyResultsService> logger,
            IIndianApiBalanceSheetClient? indianApiClient = null,
            IYahooFinanceClient? yahooFinanceClient = null)
        {
            _stockRepository = stockRepository;
            _financialRepository = financialRepository;
            _companyRepository = companyRepository;
            _mapper = mapper;
            _logger = logger;
            _indianApiClient = indianApiClient;
            _yahooFinanceClient = yahooFinanceClient;
        }

        public async Task<StockQuarterlyResultsResponseDto> GetQuarterlyResultsByStockIdAsync(
            int stockId,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            var stock = await _stockRepository.GetByIdAsync(stockId);
            if (stock == null)
            {
                throw new KeyNotFoundException($"Stock with ID {stockId} was not found.");
            }

            return await ProcessQuarterlyResultsAsync(stock, forceRefresh, cancellationToken);
        }

        public async Task<StockQuarterlyResultsResponseDto> GetQuarterlyResultsBySymbolAsync(
            string symbol,
            string? exchange = "NSE",
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("Stock symbol is required.", nameof(symbol));
            }

            var cleanSymbol = symbol.Trim().ToUpperInvariant();
            var cleanExchange = string.IsNullOrWhiteSpace(exchange) ? "NSE" : exchange.Trim().ToUpperInvariant();

            var stock = await _stockRepository.GetOrCreateStockAsync(cleanSymbol, cleanExchange, cancellationToken: cancellationToken);

            return await ProcessQuarterlyResultsAsync(stock, forceRefresh, cancellationToken);
        }

        private async Task<StockQuarterlyResultsResponseDto> ProcessQuarterlyResultsAsync(
            Stock stock,
            bool forceRefresh,
            CancellationToken cancellationToken)
        {
            var semaphore = StockLocks.GetOrAdd(stock.Id, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                var existingQuarters = await _financialRepository.GetFinancialsByStockIdAsync(stock.Id, "quarterly", limit: 12);

                var isStale = existingQuarters.Count == 0 ||
                              existingQuarters.All(q => (DateTime.UtcNow - q.LastSyncedAt).TotalHours > 24) ||
                              existingQuarters.Any(q => q.Depreciation == null && q.Revenue.HasValue) ||
                              existingQuarters.GroupBy(q => q.PeriodEndDate.HasValue ? q.PeriodEndDate.Value.ToString("yyyy-MM") : q.FiscalYear).Any(g => g.Count() > 1);

                if (forceRefresh || isStale)
                {
                    await SyncQuarterlyResultsAsync(stock, cancellationToken);
                    existingQuarters = await _financialRepository.GetFinancialsByStockIdAsync(stock.Id, "quarterly", limit: 12);
                }

                return BuildResponseDto(stock, existingQuarters);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task SyncQuarterlyResultsAsync(Stock stock, CancellationToken cancellationToken)
        {
            var syncedFromIndianApi = false;

            // Tier 1: Try IndianAPI
            if (_indianApiClient != null)
            {
                try
                {
                    _logger.LogInformation("Tier 1: Fetching quarterly financials from IndianAPI for {Symbol}", stock.Symbol);
                    var overview = await _indianApiClient.GetStockFinancialsAndOverviewAsync(stock.Symbol, stock.Exchange, cancellationToken);

                    if (overview != null && overview.Financials != null && overview.Financials.Count > 0)
                    {
                        var quarterlyPeriods = overview.Financials
                            .Where(f => f.PeriodType.Equals("quarterly", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (quarterlyPeriods.Count > 0)
                        {
                            foreach (var q in quarterlyPeriods)
                            {
                                await UpsertQuarterlyRecordAsync(stock.Id, q, "IndianAPI");
                            }
                            await _financialRepository.SaveChangesAsync();
                            syncedFromIndianApi = true;
                            _logger.LogInformation("Successfully synced {Count} quarterly records from IndianAPI for {Symbol}", quarterlyPeriods.Count, stock.Symbol);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Tier 1 IndianAPI quarterly fetch failed for {Symbol}, proceeding to Tier 2 Yahoo Finance fallback.", stock.Symbol);
                }
            }

            if (syncedFromIndianApi) return;

            // Tier 2: Try Yahoo Finance
            if (_yahooFinanceClient != null)
            {
                try
                {
                    _logger.LogInformation("Tier 2: Fetching quarterly income statements from Yahoo Finance for {Symbol}", stock.Symbol);
                    var yfQuarters = await _yahooFinanceClient.GetQuarterlyIncomeStatementsAsync(stock.Symbol, stock.Exchange, cancellationToken);

                    if (yfQuarters != null && yfQuarters.Count > 0)
                    {
                        foreach (var q in yfQuarters)
                        {
                            await UpsertQuarterlyRecordAsync(stock.Id, q, "YahooFinance");
                        }
                        await _financialRepository.SaveChangesAsync();
                        _logger.LogInformation("Successfully synced {Count} quarterly records from Yahoo Finance for {Symbol}", yfQuarters.Count, stock.Symbol);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Tier 2 Yahoo Finance quarterly fetch failed for {Symbol}.", stock.Symbol);
                }
            }
        }

        private async Task UpsertQuarterlyRecordAsync(int stockId, IndianApiFinancialPeriodDto dto, string source)
        {
            var periodKey = ResolveQuarterlyPeriodKey(dto);
            var normalizedPeriod = dto.PeriodEndDate.HasValue
                ? dto.PeriodEndDate.Value.ToString("MMM yyyy")
                : (!string.IsNullOrWhiteSpace(dto.FiscalYear) ? dto.FiscalYear : "Quarter");

            var existing = await _financialRepository.GetByStockIdAndPeriodKeyAsync(stockId, periodKey);
            if (existing == null && !string.IsNullOrWhiteSpace(dto.FiscalYear))
            {
                var altKey = $"quarterly-{dto.FiscalYear.Trim().Replace(" ", "-").ToLowerInvariant()}";
                if (!string.Equals(altKey, periodKey, StringComparison.OrdinalIgnoreCase))
                {
                    existing = await _financialRepository.GetByStockIdAndPeriodKeyAsync(stockId, altKey);
                }
            }

            if (existing == null)
            {
                var newEntity = new StockFinancial
                {
                    StockId = stockId,
                    PeriodKey = periodKey,
                    PeriodType = "quarterly",
                    FiscalYear = normalizedPeriod,
                    PeriodEndDate = dto.PeriodEndDate,
                    Revenue = dto.Revenue,
                    Expenses = dto.Expenses,
                    OperatingProfit = dto.OperatingProfit,
                    OperatingProfitMargin = dto.OperatingProfitMargin,
                    OtherIncome = dto.OtherIncome,
                    Interest = dto.Interest,
                    Depreciation = dto.Depreciation,
                    ProfitBeforeTax = dto.ProfitBeforeTax,
                    Tax = dto.Tax,
                    TaxPercentage = dto.TaxPercentage,
                    NetProfit = dto.NetProfit,
                    Eps = dto.Eps,
                    ConsolidationType = dto.ConsolidationType ?? "consolidated",
                    Source = source,
                    LastSyncedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _financialRepository.AddAsync(newEntity);
            }
            else
            {
                existing.PeriodKey = periodKey;
                existing.FiscalYear = normalizedPeriod;
                existing.PeriodEndDate = dto.PeriodEndDate ?? existing.PeriodEndDate;
                existing.Revenue = dto.Revenue ?? existing.Revenue;
                existing.Expenses = dto.Expenses ?? existing.Expenses;
                existing.OperatingProfit = dto.OperatingProfit ?? existing.OperatingProfit;
                existing.OperatingProfitMargin = dto.OperatingProfitMargin ?? existing.OperatingProfitMargin;
                existing.OtherIncome = dto.OtherIncome ?? existing.OtherIncome;
                existing.Interest = dto.Interest ?? existing.Interest;
                existing.Depreciation = dto.Depreciation ?? existing.Depreciation;
                existing.ProfitBeforeTax = dto.ProfitBeforeTax ?? existing.ProfitBeforeTax;
                existing.Tax = dto.Tax ?? existing.Tax;
                existing.TaxPercentage = dto.TaxPercentage ?? existing.TaxPercentage;
                existing.NetProfit = dto.NetProfit ?? existing.NetProfit;
                existing.Eps = dto.Eps ?? existing.Eps;
                existing.ConsolidationType = dto.ConsolidationType ?? existing.ConsolidationType;
                existing.Source = source;
                existing.LastSyncedAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;
                await _financialRepository.UpdateAsync(existing);
            }
        }

        private static string ResolveQuarterlyPeriodKey(IndianApiFinancialPeriodDto dto)
        {
            if (dto.PeriodEndDate.HasValue)
            {
                return $"quarterly-{dto.PeriodEndDate.Value:yyyy-MM-dd}";
            }
            if (!string.IsNullOrWhiteSpace(dto.FiscalYear))
            {
                if (DateTime.TryParse(dto.FiscalYear, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    return $"quarterly-{parsedDate:yyyy-MM-dd}";
                }
                var clean = dto.FiscalYear.Trim().Replace(" ", "-").ToLowerInvariant();
                return $"quarterly-{clean}";
            }
            return $"quarterly-{Guid.NewGuid():N}";
        }

        private StockQuarterlyResultsResponseDto BuildResponseDto(Stock stock, List<StockFinancial> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                return new StockQuarterlyResultsResponseDto
                {
                    StockId = stock.Id,
                    Symbol = stock.Symbol,
                    Exchange = stock.Exchange,
                    CompanyName = stock.Company?.CompanyName ?? stock.Symbol,
                    LatestQuarter = "—",
                    Source = "—",
                    LastSyncedAt = DateTime.UtcNow,
                    Summary = new QuarterlyRecordDto
                    {
                        Period = "—"
                    },
                    History = new List<QuarterlyRecordDto>()
                };
            }

            // Deduplicate and merge any duplicate entries for the same quarter period
            var historyDtos = entities
                .GroupBy(e => e.PeriodEndDate.HasValue
                    ? e.PeriodEndDate.Value.ToString("yyyy-MM")
                    : (!string.IsNullOrWhiteSpace(e.FiscalYear) ? e.FiscalYear.Trim().ToUpperInvariant() : e.PeriodKey))
                .Select(g =>
                {
                    var primary = g.OrderByDescending(x => x.LastSyncedAt).First();
                    var mergedDto = _mapper.Map<QuarterlyRecordDto>(primary);

                    foreach (var other in g)
                    {
                        mergedDto.Sales ??= other.Revenue;
                        mergedDto.Expenses ??= other.Expenses;
                        mergedDto.OperatingProfit ??= other.OperatingProfit;
                        mergedDto.OpmPercentage ??= other.OperatingProfitMargin;
                        mergedDto.OtherIncome ??= other.OtherIncome;
                        mergedDto.Interest ??= other.Interest;
                        mergedDto.Depreciation ??= other.Depreciation;
                        mergedDto.ProfitBeforeTax ??= other.ProfitBeforeTax;
                        mergedDto.Tax ??= other.Tax;
                        mergedDto.TaxPercentage ??= other.TaxPercentage;
                        mergedDto.NetProfit ??= other.NetProfit;
                        mergedDto.Eps ??= other.Eps;
                    }

                    if (primary.PeriodEndDate.HasValue)
                    {
                        mergedDto.Period = primary.PeriodEndDate.Value.ToString("MMM yyyy");
                    }
                    else if (string.IsNullOrWhiteSpace(mergedDto.Period))
                    {
                        mergedDto.Period = primary.FiscalYear ?? "Quarter";
                    }

                    return new
                    {
                        Date = primary.PeriodEndDate ?? DateTime.MinValue,
                        FiscalYear = primary.FiscalYear ?? "",
                        Merged = mergedDto
                    };
                })
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.FiscalYear)
                .Select(x => x.Merged)
                .ToList();

            var latest = historyDtos.FirstOrDefault() ?? new QuarterlyRecordDto { Period = "—" };

            var response = new StockQuarterlyResultsResponseDto
            {
                StockId = stock.Id,
                Symbol = stock.Symbol,
                Exchange = stock.Exchange,
                CompanyName = stock.Company?.CompanyName ?? stock.Symbol,
                LatestQuarter = latest.Period,
                Source = entities.FirstOrDefault()?.Source ?? "—",
                LastSyncedAt = entities.FirstOrDefault()?.LastSyncedAt ?? DateTime.UtcNow,
                Summary = latest,
                History = historyDtos
            };

            // Calculate QoQ Growth (Latest vs Previous Quarter)
            if (historyDtos.Count >= 2)
            {
                var prev = historyDtos[1];
                response.QoQGrowth = new QuarterlyGrowthDto
                {
                    SalesGrowthPercent = CalculateGrowth(latest.Sales, prev.Sales),
                    OperatingProfitGrowthPercent = CalculateGrowth(latest.OperatingProfit, prev.OperatingProfit),
                    NetProfitGrowthPercent = CalculateGrowth(latest.NetProfit, prev.NetProfit),
                    EpsGrowthPercent = CalculateGrowth(latest.Eps, prev.Eps),
                    DepreciationGrowthPercent = CalculateGrowth(latest.Depreciation, prev.Depreciation),
                    TaxGrowthPercent = CalculateGrowth(latest.Tax, prev.Tax)
                };
            }

            // Calculate YoY Growth (Latest vs 4 Quarters Ago)
            if (historyDtos.Count >= 5)
            {
                var yoy = historyDtos[4];
                response.YoYGrowth = new QuarterlyGrowthDto
                {
                    SalesGrowthPercent = CalculateGrowth(latest.Sales, yoy.Sales),
                    OperatingProfitGrowthPercent = CalculateGrowth(latest.OperatingProfit, yoy.OperatingProfit),
                    NetProfitGrowthPercent = CalculateGrowth(latest.NetProfit, yoy.NetProfit),
                    EpsGrowthPercent = CalculateGrowth(latest.Eps, yoy.Eps),
                    DepreciationGrowthPercent = CalculateGrowth(latest.Depreciation, yoy.Depreciation),
                    TaxGrowthPercent = CalculateGrowth(latest.Tax, yoy.Tax)
                };
            }

            return response;
        }

        private static decimal? CalculateGrowth(decimal? current, decimal? previous)
        {
            if (!current.HasValue || !previous.HasValue || previous.Value == 0)
            {
                return null;
            }

            var growth = ((current.Value - previous.Value) / Math.Abs(previous.Value)) * 100m;
            return Math.Round(growth, 2);
        }
    }
}
