using AutoMapper;
using Microsoft.Extensions.Logging;
using StockLens_BusinessLayer.DTOs;
using StockLens_BusinessLayer.Interfaces;
using StockLens_DataLayer.Entities;
using StockLens_DataLayer.Interfaces;
using StockLens_Infrastructure.ExternalServices.BharatStock;
using StockLens_Infrastructure.ExternalServices.BharatStock.Exceptions;
using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Services
{
    public class StockCashflowService : IStockCashflowService
    {
        private readonly IStockRepository _stockRepository;
        private readonly IStockFinancialRepository _financialRepository;
        private readonly ICompanyRepository _companyRepository;
        private readonly IFinancialProvider _financialProvider;
        private readonly IMapper _mapper;
        private readonly ILogger<StockCashflowService> _logger;

        private static readonly ConcurrentDictionary<int, SemaphoreSlim> StockLocks = new();

        public StockCashflowService(
            IStockRepository stockRepository,
            IStockFinancialRepository financialRepository,
            ICompanyRepository companyRepository,
            IFinancialProvider financialProvider,
            IMapper mapper,
            ILogger<StockCashflowService> logger)
        {
            _stockRepository = stockRepository;
            _financialRepository = financialRepository;
            _companyRepository = companyRepository;
            _financialProvider = financialProvider;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<StockCashflowResponseDto> GetCashflowByStockIdAsync(
            int stockId,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            var stock = await _stockRepository.GetByIdAsync(stockId);
            if (stock == null)
            {
                throw new KeyNotFoundException($"Stock with ID {stockId} was not found.");
            }

            return await ProcessCashflowAsync(stock, forceRefresh, cancellationToken);
        }

        public async Task<StockCashflowResponseDto> GetCashflowBySymbolAsync(
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

            return await ProcessCashflowAsync(stock, forceRefresh, cancellationToken);
        }

        private async Task<StockCashflowResponseDto> ProcessCashflowAsync(
            Stock stock,
            bool forceRefresh,
            CancellationToken cancellationToken)
        {
            var existingEntities = await _financialRepository.GetFinancialsByStockIdAsync(stock.Id, "annual", 3);

            if (!forceRefresh && existingEntities.Count > 0)
            {
                _logger.LogInformation("Serving {Count} annual financial records from DB cache for stock {Symbol} (StockId: {StockId}).",
                    existingEntities.Count, stock.Symbol, stock.Id);
                return BuildResponseDto(stock, existingEntities);
            }

            // Acquire per-stock lock to prevent thundering herd API calls
            var stockLock = StockLocks.GetOrAdd(stock.Id, _ => new SemaphoreSlim(1, 1));
            await stockLock.WaitAsync(cancellationToken);

            try
            {
                if (!forceRefresh)
                {
                    existingEntities = await _financialRepository.GetFinancialsByStockIdAsync(stock.Id, "annual", 3);
                    if (existingEntities.Count > 0)
                    {
                        return BuildResponseDto(stock, existingEntities);
                    }
                }

                _logger.LogInformation("Syncing annual financials from BharatStock for stock {Symbol} (StockId: {StockId}, ForceRefresh: {ForceRefresh}).",
                    stock.Symbol, stock.Id, forceRefresh);

                await SyncFinancialsFromProviderAsync(stock, cancellationToken);

                existingEntities = await _financialRepository.GetFinancialsByStockIdAsync(stock.Id, "annual", 3);
                if (existingEntities.Count == 0)
                {
                    throw new ProviderNotFoundException(stock.Symbol, $"Annual financial and cashflow data is not available for {stock.Symbol}.");
                }

                return BuildResponseDto(stock, existingEntities);
            }
            finally
            {
                stockLock.Release();
            }
        }

        private async Task SyncFinancialsFromProviderAsync(Stock stock, CancellationToken cancellationToken)
        {
            IReadOnlyList<BharatStockFinancialRecord>? records;

            try
            {
                records = await _financialProvider.GetFinancialsAsync(stock.Symbol, "annual", 1, 3, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve financials from BharatStock provider for stock {Symbol}.", stock.Symbol);
                throw;
            }

            if (records == null || records.Count == 0)
            {
                _logger.LogWarning("No financial records returned by BharatStock for {Symbol}.", stock.Symbol);
                return;
            }

            var now = DateTime.UtcNow;

            foreach (var record in records)
            {
                var periodKey = record.ResolvedPeriodKey;
                if (string.IsNullOrWhiteSpace(periodKey))
                {
                    continue;
                }

                var cfo = record.CashFlowOperating;
                var capex = record.Capex;
                decimal? fcf = null;
                if (cfo.HasValue && capex.HasValue)
                {
                    fcf = cfo.Value - capex.Value;
                }
                else if (cfo.HasValue)
                {
                    fcf = cfo.Value;
                }

                var existing = await _financialRepository.GetByStockIdAndPeriodKeyAsync(stock.Id, periodKey);
                if (existing != null)
                {
                    existing.FiscalYear = record.ResolvedFiscalYear;
                    existing.PeriodType = record.PeriodType ?? "annual";
                    existing.PeriodEndDate = record.ResolvedPeriodEndDate ?? existing.PeriodEndDate;

                    if (record.Revenue.HasValue) existing.Revenue = record.Revenue;
                    if (record.NetProfit.HasValue) existing.NetProfit = record.NetProfit;
                    if (record.Eps.HasValue) existing.Eps = record.Eps;
                    if (record.NetProfitAttributableToMinorityInterest.HasValue)
                        existing.NetProfitAttributableToMinorityInterest = record.NetProfitAttributableToMinorityInterest;
                    if (record.OtherEquity.HasValue) existing.OtherEquity = record.OtherEquity;
                    if (cfo.HasValue) existing.OperatingCashFlow = cfo;
                    if (capex.HasValue) existing.Capex = capex;
                    if (fcf.HasValue) existing.FreeCashFlow = fcf;
                    if (record.NetCashFlow.HasValue) existing.NetCashFlow = record.NetCashFlow;
                    if (!string.IsNullOrWhiteSpace(record.ConsolidationType)) existing.ConsolidationType = record.ConsolidationType;

                    existing.Source = !string.IsNullOrWhiteSpace(record.Source) ? record.Source : existing.Source;
                    existing.LastSyncedAt = now;
                    existing.UpdatedAt = now;

                    await _financialRepository.UpdateAsync(existing);
                }
                else
                {
                    var newEntity = new StockFinancial
                    {
                        StockId = stock.Id,
                        PeriodKey = periodKey,
                        PeriodType = record.PeriodType ?? "annual",
                        FiscalYear = record.ResolvedFiscalYear,
                        PeriodEndDate = record.ResolvedPeriodEndDate,
                        Revenue = record.Revenue,
                        NetProfit = record.NetProfit,
                        Eps = record.Eps,
                        NetProfitAttributableToMinorityInterest = record.NetProfitAttributableToMinorityInterest,
                        OtherEquity = record.OtherEquity,
                        OperatingCashFlow = cfo,
                        Capex = capex,
                        FreeCashFlow = fcf,
                        NetCashFlow = record.NetCashFlow,
                        ConsolidationType = record.ConsolidationType ?? "consolidated",
                        Source = !string.IsNullOrWhiteSpace(record.Source) ? record.Source : "BharatStock",
                        LastSyncedAt = now,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    await _financialRepository.AddAsync(newEntity);
                }
            }

            await _financialRepository.SaveChangesAsync();
        }

        private StockCashflowResponseDto BuildResponseDto(Stock stock, List<StockFinancial> entities)
        {
            if (entities.Count == 0)
            {
                throw new KeyNotFoundException($"No financial records found for stock {stock.Symbol}.");
            }

            var historyDtos = entities.Take(3).Select(e =>
            {
                var dto = _mapper.Map<AnnualCashflowItemDto>(e);
                dto.DataAsOf = e.PeriodEndDate?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? e.FiscalYear;
                dto.PeriodEndDate = e.PeriodEndDate?.ToString("yyyy-MM-dd");

                // Derived calculations
                if (dto.OperatingCashFlow.HasValue && dto.NetProfit.HasValue && dto.NetProfit.Value != 0)
                {
                    dto.CfoToNetProfitRatio = Math.Round(dto.OperatingCashFlow.Value / dto.NetProfit.Value, 2);
                }

                if (dto.FreeCashFlow.HasValue && dto.Revenue.HasValue && dto.Revenue.Value != 0)
                {
                    dto.FcfMarginPercent = Math.Round((dto.FreeCashFlow.Value / dto.Revenue.Value) * 100, 2);
                }

                if (dto.Capex.HasValue && dto.OperatingCashFlow.HasValue && dto.OperatingCashFlow.Value != 0)
                {
                    dto.CapexToCfoPercent = Math.Round((dto.Capex.Value / dto.OperatingCashFlow.Value) * 100, 2);
                }

                return dto;
            }).ToList();

            var current = historyDtos[0];
            AnnualCashflowItemDto? previous = historyDtos.Count > 1 ? historyDtos[1] : null;

            var yoy = new CashflowYoYChangeDto();

            if (previous != null)
            {
                yoy.FreeCashFlowChange = CalculateAbsoluteChange(current.FreeCashFlow, previous.FreeCashFlow);
                yoy.FreeCashFlowGrowth = CalculatePercentageGrowth(current.FreeCashFlow, previous.FreeCashFlow);

                yoy.OperatingCashFlowChange = CalculateAbsoluteChange(current.OperatingCashFlow, previous.OperatingCashFlow);
                yoy.OperatingCashFlowGrowth = CalculatePercentageGrowth(current.OperatingCashFlow, previous.OperatingCashFlow);

                yoy.CapexChange = CalculateAbsoluteChange(current.Capex, previous.Capex);
                yoy.CapexGrowth = CalculatePercentageGrowth(current.Capex, previous.Capex);

                yoy.NetProfitChange = CalculateAbsoluteChange(current.NetProfit, previous.NetProfit);
                yoy.NetProfitGrowth = CalculatePercentageGrowth(current.NetProfit, previous.NetProfit);

                yoy.RevenueChange = CalculateAbsoluteChange(current.Revenue, previous.Revenue);
                yoy.RevenueGrowth = CalculatePercentageGrowth(current.Revenue, previous.Revenue);

                yoy.NetCashFlowChange = CalculateAbsoluteChange(current.NetCashFlow, previous.NetCashFlow);
                yoy.NetCashFlowGrowth = CalculatePercentageGrowth(current.NetCashFlow, previous.NetCashFlow);
            }

            var summary = new CashflowSummaryDto
            {
                FiscalYear = current.FiscalYear,
                PeriodEndDate = current.PeriodEndDate,
                OperatingCashFlow = current.OperatingCashFlow,
                Capex = current.Capex,
                FreeCashFlow = current.FreeCashFlow,
                NetCashFlow = current.NetCashFlow,
                Revenue = current.Revenue,
                NetProfit = current.NetProfit,
                Eps = current.Eps,
                OtherEquity = current.OtherEquity,
                CfoToNetProfitRatio = current.CfoToNetProfitRatio,
                FcfMarginPercent = current.FcfMarginPercent,
                CapexToCfoPercent = current.CapexToCfoPercent,
                ConsolidationType = current.ConsolidationType,
                YoYChange = yoy
            };

            var latestEntity = entities[0];

            return new StockCashflowResponseDto
            {
                StockId = stock.Id,
                Symbol = stock.Symbol,
                Exchange = stock.Exchange,
                CompanyName = stock.Company?.CompanyName ?? $"{stock.Symbol} Limited",
                LatestFiscalYear = current.FiscalYear,
                DataAsOf = current.DataAsOf,
                Source = latestEntity.Source,
                LastSyncedAt = latestEntity.LastSyncedAt,
                Summary = summary,
                History = historyDtos
            };
        }

        private static decimal? CalculateAbsoluteChange(decimal? current, decimal? previous)
        {
            if (current.HasValue && previous.HasValue)
            {
                return Math.Round(current.Value - previous.Value, 2);
            }
            return null;
        }

        private static decimal? CalculatePercentageGrowth(decimal? current, decimal? previous)
        {
            if (current.HasValue && previous.HasValue && previous.Value != 0)
            {
                var growth = ((current.Value - previous.Value) / Math.Abs(previous.Value)) * 100;
                return Math.Round(growth, 2);
            }
            return null;
        }
    }
}
