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

using StockLens_Infrastructure.ExternalServices.IndianApi;
using StockLens_Infrastructure.ExternalServices.YahooFinanceApi;

namespace StockLens_BusinessLayer.Services
{
    public class StockCashflowService : IStockCashflowService
    {
        private readonly IStockRepository _stockRepository;
        private readonly IStockFinancialRepository _financialRepository;
        private readonly ICompanyRepository _companyRepository;
        private readonly IStockBalanceSheetRepository _balanceSheetRepository;
        private readonly IFinancialProvider _financialProvider;
        private readonly ISectorValuationService _sectorValuationService;
        private readonly IMapper _mapper;
        private readonly ILogger<StockCashflowService> _logger;
        private readonly IIndianApiBalanceSheetClient? _indianApiClient;
        private readonly IYahooFinanceClient? _yahooFinanceClient;

        private static readonly ConcurrentDictionary<int, SemaphoreSlim> StockLocks = new();

        public StockCashflowService(
            IStockRepository stockRepository,
            IStockFinancialRepository financialRepository,
            ICompanyRepository companyRepository,
            IStockBalanceSheetRepository balanceSheetRepository,
            IFinancialProvider financialProvider,
            ISectorValuationService sectorValuationService,
            IMapper mapper,
            ILogger<StockCashflowService> logger,
            IIndianApiBalanceSheetClient? indianApiClient = null,
            IYahooFinanceClient? yahooFinanceClient = null)
        {
            _stockRepository = stockRepository;
            _financialRepository = financialRepository;
            _companyRepository = companyRepository;
            _balanceSheetRepository = balanceSheetRepository;
            _financialProvider = financialProvider;
            _sectorValuationService = sectorValuationService;
            _mapper = mapper;
            _logger = logger;
            _indianApiClient = indianApiClient;
            _yahooFinanceClient = yahooFinanceClient;
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

            var stock = await _stockRepository.GetOrCreateStockAsync(cleanSymbol, cleanExchange, cancellationToken: cancellationToken);

            return await ProcessCashflowAsync(stock, forceRefresh, cancellationToken);
        }

        public async Task<StockRatiosDto?> GetRatiosBySymbolAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("Stock symbol is required.", nameof(symbol));
            }

            var cleanSymbol = symbol.Trim().ToUpperInvariant();

            // 1. Check if DB has cached financial records with ratios
            var stock = await _stockRepository.GetBySymbolAsync(cleanSymbol);
            if (stock != null)
            {
                var dbFinancials = await _financialRepository.GetFinancialsByStockIdAsync(stock.Id, "annual", 1);
                if (dbFinancials != null && dbFinancials.Count > 0 && dbFinancials[0].Roce.HasValue && dbFinancials[0].PeRatio.HasValue)
                {
                    var cur = dbFinancials[0];
                    var dbBs = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 1);
                    var latestBsDb = dbBs.FirstOrDefault();
                    decimal? eqCapDb = latestBsDb?.EquityCapital ?? cur.EquityCapital;

                    // Always fetch and apply real-time live price
                    var liveQuote = await GetLiveQuoteInternalAsync(cleanSymbol, stock.Exchange, cancellationToken);
                    if (liveQuote?.Price.HasValue == true && liveQuote.Price.Value > 0)
                    {
                        cur.CurrentPrice = liveQuote.Price.Value;
                        if (liveQuote.YearHigh.HasValue) cur.Week52High = liveQuote.YearHigh;
                        if (liveQuote.YearLow.HasValue) cur.Week52Low = liveQuote.YearLow;
                        cur.LastSyncedAt = DateTime.UtcNow;
                        cur.RatiosAsOfDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                        if (cur.TtmEps.HasValue && cur.TtmEps.Value > 0)
                            cur.PeRatio = Math.Round(cur.CurrentPrice.Value / cur.TtmEps.Value, 2);
                        if (cur.BookValue.HasValue && cur.BookValue.Value > 0)
                            cur.PbRatio = Math.Round(cur.CurrentPrice.Value / cur.BookValue.Value, 2);

                        var (refreshedMc, refreshedShares, refreshedEqCap, refreshedMcSrc) = CalculateMarketCap(eqCapDb, cur.FaceValue, cur.CurrentPrice, cur.MarketCap);
                        cur.MarketCap = refreshedMc;
                        cur.MarketCapSource = refreshedMcSrc;

                        try
                        {
                            await _financialRepository.UpdateAsync(cur);
                            await _financialRepository.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to persist refreshed live price to DB for {Symbol}", cleanSymbol);
                        }
                    }

                    var (totEq, eqSrc, eqPer) = DetermineTotalEquity(null, latestBsDb);
                    var (mCap, tShares, eqCapOut, mSource) = CalculateMarketCap(eqCapDb, cur.FaceValue, cur.CurrentPrice, cur.MarketCap);

                    return new StockRatiosDto
                    {
                        Roe = cur.Roe,
                        Roce = cur.Roce,
                        PeRatio = cur.PeRatio,
                        TtmEps = cur.TtmEps,
                        PbRatio = cur.PbRatio,
                        DividendYield = cur.DividendYield,
                        Week52High = cur.Week52High,
                        Week52Low = cur.Week52Low,
                        CurrentPrice = cur.CurrentPrice,
                        AsOfDate = cur.RatiosAsOfDate,
                        FinancialsFiscalYear = cur.FiscalYear,
                        FinancialsPeriodType = cur.PeriodType,
                        FaceValue = cur.FaceValue,
                        EquityCapital = eqCapOut,
                        TotalShares = tShares,
                        TotalEquity = totEq ?? cur.TotalEquity,
                        TotalEquityPeriod = eqPer ?? cur.FiscalYear,
                        TotalEquitySource = eqSrc ?? cur.TotalEquitySource,
                        BookValue = cur.BookValue,
                        MarketCap = mCap,
                        MarketCapSource = mSource,
                        SectorPe = cur.SectorPe,
                        SectorPeSector = cur.SectorPeSector,
                        SectorPeAsOfDate = cur.RatiosAsOfDate
                    };
                }
            }

            // 2. Primary: Try IndianAPI if available
            if (_indianApiClient != null)
            {
                try
                {
                    var indianData = await _indianApiClient.GetStockFinancialsAndOverviewAsync(cleanSymbol, "NSE", cancellationToken);
                    if (indianData != null)
                    {
                        StockBalanceSheet? latestBsDb = null;
                        if (stock != null)
                        {
                            var dbBs = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 1);
                            latestBsDb = dbBs.FirstOrDefault();
                        }
                        decimal? eqCap = latestBsDb?.EquityCapital;
                        var (marketCap, totalShares, eqCapVal, mcSource) = CalculateMarketCap(eqCap, indianData.FaceValue, indianData.CurrentPrice, indianData.MarketCap);

                        return new StockRatiosDto
                        {
                            Roe = indianData.Roe,
                            Roce = indianData.Roce,
                            PeRatio = indianData.PeRatio,
                            TtmEps = indianData.TtmEps,
                            PbRatio = indianData.PbRatio,
                            DividendYield = indianData.DividendYield,
                            Week52High = indianData.YearHigh,
                            Week52Low = indianData.YearLow,
                            CurrentPrice = indianData.CurrentPrice,
                            FinancialsFiscalYear = indianData.Financials.FirstOrDefault()?.FiscalYear,
                            FinancialsPeriodType = indianData.Financials.FirstOrDefault()?.PeriodType ?? "annual",
                            FaceValue = indianData.FaceValue,
                            EquityCapital = eqCapVal ?? eqCap,
                            TotalShares = totalShares,
                            BookValue = indianData.BookValue,
                            MarketCap = marketCap,
                            MarketCapSource = mcSource,
                            SectorPe = indianData.SectorPe,
                            SectorPeSector = indianData.SectorName
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch ratios directly from IndianAPI for {Symbol}. Attempting fallback.", cleanSymbol);
                }
            }

            // 3. Fallback: Backup Provider
            BharatStockRatiosRecord? ratios = null;
            BharatStockCompanyDetailsRecord? details = null;
            BharatStockScreenerRecord? screener = null;
            IReadOnlyList<BharatStockFinancialRecord>? financials = null;
            decimal? livePrice = null;

            try
            {
                var ratiosTask = _financialProvider.GetRatiosAsync(cleanSymbol, cancellationToken);
                var detailsTask = _financialProvider.GetStockDetailsAsync(cleanSymbol, "NSE", cancellationToken);
                var screenerTask = _financialProvider.GetScreenerDataAsync(cleanSymbol, "NSE", cancellationToken);
                var financialsTask = _financialProvider.GetFinancialsAsync(cleanSymbol, "annual", 1, 1, cancellationToken);

                await Task.WhenAll(ratiosTask, detailsTask, screenerTask, financialsTask);
                ratios = await ratiosTask;
                details = await detailsTask;
                screener = await screenerTask;
                financials = await financialsTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Backup provider ratios fetch failed for {Symbol}.", cleanSymbol);
            }

            if (ratios == null && details == null && screener == null && (financials == null || financials.Count == 0) && livePrice == null) return null;

            var latestFinancial = financials != null && financials.Count > 0 ? financials[0] : null;
            StockBalanceSheet? latestBs = null;
            if (stock != null)
            {
                var dbBs = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 1);
                latestBs = dbBs.FirstOrDefault();
            }
            var (totalEquity, equitySource, equityPeriod) = DetermineTotalEquity(latestFinancial, latestBs);

            SectorValuationResultDto? sectorValuation = null;
            var sector = details?.Sector ?? screener?.Sector;
            if (!string.IsNullOrWhiteSpace(sector))
            {
                try
                {
                    sectorValuation = await _sectorValuationService.GetSectorValuationAsync(sector, "NSE", cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to compute sector valuation for {Sector}.", sector);
                }
            }

            var currentPrice = livePrice ?? ratios?.Price ?? details?.LatestPrice?.Close ?? screener?.Price;
            var faceValue = details?.FaceValue;
            var equityCapital = latestBs?.EquityCapital;
            var (marketCapFallback, totalSharesFallback, eqCapFallback, mcSourceFallback) = CalculateMarketCap(equityCapital, faceValue, currentPrice, screener?.MarketCap);

            return new StockRatiosDto
            {
                Roe = ratios?.Roe ?? screener?.Roe,
                Roce = ratios?.Roce ?? screener?.Roce,
                PeRatio = ratios?.PeRatio ?? screener?.PeRatio,
                PbRatio = ratios?.PbRatio ?? screener?.PbRatio,
                DividendYield = ratios?.DividendYield,
                Week52High = ratios?.Week52High,
                Week52Low = ratios?.Week52Low,
                CurrentPrice = currentPrice,
                AsOfDate = ratios?.AsOfDate ?? details?.LatestPrice?.TradeDate ?? screener?.ComputedAt,
                FinancialsFiscalYear = ratios?.FinancialsFiscalYear ?? latestFinancial?.ResolvedFiscalYear,
                FinancialsPeriodType = ratios?.FinancialsPeriodType ?? latestFinancial?.PeriodType,
                FaceValue = faceValue,
                EquityCapital = eqCapFallback,
                TotalShares = totalSharesFallback,
                TotalEquity = totalEquity,
                TotalEquityPeriod = equityPeriod,
                TotalEquitySource = equitySource,
                BookValue = screener?.ResolvedBookValue,
                MarketCap = marketCapFallback,
                MarketCapSource = mcSourceFallback,
                SectorPe = sectorValuation?.SectorPe,
                SectorPeSector = sectorValuation?.Sector ?? sector,
                SectorPeAsOfDate = sectorValuation?.AsOfDate
            };
        }

        private async Task<StockCashflowResponseDto> ProcessCashflowAsync(
            Stock stock,
            bool forceRefresh,
            CancellationToken cancellationToken)
        {
            var existingEntities = await _financialRepository.GetFinancialsByStockIdAsync(stock.Id, "annual", 3);

            bool isFresh = false;
            if (existingEntities.Count > 0)
            {
                var lastSync = existingEntities.Max(e => e.LastSyncedAt);
                isFresh = (DateTime.UtcNow - lastSync).TotalDays < 7;
            }

            if (!forceRefresh && existingEntities.Count > 0 && isFresh)
            {
                _logger.LogInformation("Serving {Count} annual financial records from DB cache for stock {Symbol} (StockId: {StockId}, LastSynced: {LastSynced}).",
                    existingEntities.Count, stock.Symbol, stock.Id, existingEntities.Max(e => e.LastSyncedAt));

                // Always check and apply real-time live price from market feed
                try
                {
                    var liveQuote = await GetLiveQuoteInternalAsync(stock.Symbol, stock.Exchange, cancellationToken);
                    if (liveQuote?.Price.HasValue == true && liveQuote.Price.Value > 0)
                    {
                        var cur = existingEntities[0];
                        cur.CurrentPrice = liveQuote.Price.Value;
                        if (liveQuote.YearHigh.HasValue) cur.Week52High = liveQuote.YearHigh;
                        if (liveQuote.YearLow.HasValue) cur.Week52Low = liveQuote.YearLow;
                        cur.LastSyncedAt = DateTime.UtcNow;
                        cur.RatiosAsOfDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                        if (cur.TtmEps.HasValue && cur.TtmEps.Value > 0)
                            cur.PeRatio = Math.Round(cur.CurrentPrice.Value / cur.TtmEps.Value, 2);
                        if (cur.BookValue.HasValue && cur.BookValue.Value > 0)
                            cur.PbRatio = Math.Round(cur.CurrentPrice.Value / cur.BookValue.Value, 2);

                        var dbBs = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 1);
                        var latestBsDb = dbBs.FirstOrDefault();
                        decimal? eqCapDb = latestBsDb?.EquityCapital ?? cur.EquityCapital;
                        var (mCap, tShares, eqCapOut, mSource) = CalculateMarketCap(eqCapDb, cur.FaceValue, cur.CurrentPrice, cur.MarketCap);
                        cur.MarketCap = mCap;
                        cur.MarketCapSource = mSource;

                        await _financialRepository.UpdateAsync(cur);
                        await _financialRepository.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to refresh real-time live price for {Symbol}", stock.Symbol);
                }

                // If cached entity doesn't have ratios or new metrics yet, lazily fetch and update
                if ((existingEntities[0].Roe == null && existingEntities[0].PeRatio == null) ||
                    existingEntities[0].FaceValue == null ||
                    existingEntities[0].BookValue == null ||
                    existingEntities[0].SectorPe == null)
                {
                    try
                    {
                        if (existingEntities[0].Source == "IndianAPI" && _indianApiClient != null)
                        {
                            var overview = await _indianApiClient.GetStockFinancialsAndOverviewAsync(stock.Symbol, stock.Exchange, cancellationToken);
                            if (overview != null)
                            {
                                var dbBs = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 1);
                                var latestBs = dbBs.FirstOrDefault();
                                ApplyIndianApiRatiosToEntity(existingEntities[0], overview, latestBs);
                                await _financialRepository.UpdateAsync(existingEntities[0]);
                                await _financialRepository.SaveChangesAsync();
                            }
                        }
                        else
                        {
                            var ratiosTask = _financialProvider.GetRatiosAsync(stock.Symbol, cancellationToken);
                            var detailsTask = _financialProvider.GetStockDetailsAsync(stock.Symbol, stock.Exchange, cancellationToken);
                            var screenerTask = _financialProvider.GetScreenerDataAsync(stock.Symbol, stock.Exchange, cancellationToken);
                            var livePriceTask = _indianApiClient != null
                                ? _indianApiClient.GetCurrentPriceAsync(stock.Symbol, stock.Exchange, cancellationToken)
                                : Task.FromResult<decimal?>(null);

                            await Task.WhenAll(ratiosTask, detailsTask, screenerTask, livePriceTask);
                            var ratios = await ratiosTask;
                            var details = await detailsTask;
                            var screener = await screenerTask;
                            var livePrice = await livePriceTask;

                            SectorValuationResultDto? sectorValuation = null;
                            var sector = details?.Sector ?? screener?.Sector;
                            if (!string.IsNullOrWhiteSpace(sector))
                            {
                                sectorValuation = await _sectorValuationService.GetSectorValuationAsync(sector, stock.Exchange, cancellationToken);
                            }

                            var dbBs = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 1);
                            var latestBs = dbBs.FirstOrDefault();

                            if (ratios != null || details != null || screener != null || sectorValuation != null || livePrice != null)
                            {
                                ApplyRatiosToEntity(existingEntities[0], ratios, details, screener, sectorValuation, null, latestBs, livePrice);
                                await _financialRepository.UpdateAsync(existingEntities[0]);
                                await _financialRepository.SaveChangesAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not lazily populate metrics for {Symbol}.", stock.Symbol);
                    }
                }

                return await BuildResponseDtoAsync(stock, existingEntities, cancellationToken);
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
                        var lastSync = existingEntities.Max(e => e.LastSyncedAt);
                        if ((DateTime.UtcNow - lastSync).TotalDays < 7)
                        {
                            return await BuildResponseDtoAsync(stock, existingEntities, cancellationToken);
                        }
                    }
                }

                _logger.LogInformation("Syncing annual financials and ratios from BharatStock for stock {Symbol} (StockId: {StockId}, ForceRefresh: {ForceRefresh}).",
                    stock.Symbol, stock.Id, forceRefresh);

                await SyncFinancialsFromProviderAsync(stock, cancellationToken);

                existingEntities = await _financialRepository.GetFinancialsByStockIdAsync(stock.Id, "annual", 3);
                if (existingEntities.Count == 0)
                {
                    throw new ProviderNotFoundException(stock.Symbol, $"Annual financial and cashflow data is not available for {stock.Symbol}.");
                }

                return await BuildResponseDtoAsync(stock, existingEntities, cancellationToken);
            }
            finally
            {
                stockLock.Release();
            }
        }

        private async Task SyncFinancialsFromProviderAsync(Stock stock, CancellationToken cancellationToken)
        {
            // 1. Primary: Try IndianAPI (Single unified call for CAS, INC, BAL, valuation metrics)
            bool indianApiSuccess = false;
            try
            {
                indianApiSuccess = await SyncFromIndianApiAsync(stock, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Primary IndianAPI sync encountered an error for stock {Symbol}. Attempting BharatStock fallback.", stock.Symbol);
                indianApiSuccess = false;
            }

            if (indianApiSuccess)
            {
                _logger.LogInformation("Successfully synced financial statements and ratios from IndianAPI for {Symbol}.", stock.Symbol);
                return;
            }

            // 2. Fallback: BharatStock
            _logger.LogInformation("Syncing annual financials and ratios from BharatStock fallback for stock {Symbol}.", stock.Symbol);
            await SyncFromBharatStockAsync(stock, cancellationToken);
        }

        private async Task<bool> SyncFromIndianApiAsync(Stock stock, CancellationToken cancellationToken)
        {
            if (_indianApiClient == null)
            {
                return false;
            }

            _logger.LogInformation("Attempting primary financial & cashflow sync from IndianAPI for {Symbol}", stock.Symbol);
            var data = await _indianApiClient.GetStockFinancialsAndOverviewAsync(stock.Symbol, stock.Exchange, cancellationToken);

            if (data == null || data.Financials.Count == 0)
            {
                _logger.LogWarning("IndianAPI returned no financial periods for {Symbol}.", stock.Symbol);
                return false;
            }

            var now = DateTime.UtcNow;
            var balanceSheets = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 5);

            var periodsToSave = data.Financials.Take(5).ToList();

            for (int i = 0; i < periodsToSave.Count; i++)
            {
                var period = periodsToSave[i];
                var periodKey = period.PeriodKey;
                if (string.IsNullOrWhiteSpace(periodKey))
                {
                    continue;
                }

                var cfo = period.OperatingCashFlow;
                var capex = period.Capex;
                var fcf = period.FreeCashFlow;

                var matchingBs = balanceSheets.FirstOrDefault(b =>
                    (!string.IsNullOrEmpty(b.FiscalYear) && b.FiscalYear.Equals(period.FiscalYear, StringComparison.OrdinalIgnoreCase)) ||
                    (b.PeriodEndDate.HasValue && period.PeriodEndDate.HasValue && b.PeriodEndDate.Value.Date == period.PeriodEndDate.Value.Date))
                    ?? (i == 0 ? balanceSheets.FirstOrDefault() : null);

                var existing = await _financialRepository.GetByStockIdAndPeriodKeyAsync(stock.Id, periodKey);
                if (existing != null)
                {
                    existing.FiscalYear = period.FiscalYear;
                    existing.PeriodType = period.PeriodType ?? "annual";
                    existing.PeriodEndDate = period.PeriodEndDate ?? existing.PeriodEndDate;

                    if (period.Revenue.HasValue) existing.Revenue = period.Revenue;
                    if (period.OperatingProfit.HasValue) existing.OperatingProfit = period.OperatingProfit;
                    if (period.NetProfit.HasValue) existing.NetProfit = period.NetProfit;
                    if (period.Eps.HasValue) existing.Eps = period.Eps;
                    if (period.NetProfitAttributableToMinorityInterest.HasValue)
                        existing.NetProfitAttributableToMinorityInterest = period.NetProfitAttributableToMinorityInterest;
                    if (period.OtherEquity.HasValue) existing.OtherEquity = period.OtherEquity;
                    if (cfo.HasValue) existing.OperatingCashFlow = cfo;
                    if (capex.HasValue) existing.Capex = capex;
                    if (fcf.HasValue) existing.FreeCashFlow = fcf;
                    if (period.NetCashFlow.HasValue) existing.NetCashFlow = period.NetCashFlow;
                    if (period.TotalEquity.HasValue) existing.TotalEquity = period.TotalEquity;
                    if (period.BookValuePerShare.HasValue) existing.BookValue = period.BookValuePerShare;
                    existing.ConsolidationType = period.ConsolidationType ?? "consolidated";

                    if (i == 0)
                    {
                        ApplyIndianApiRatiosToEntity(existing, data, matchingBs);
                    }

                    existing.Source = "IndianAPI";
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
                        PeriodType = period.PeriodType ?? "annual",
                        FiscalYear = period.FiscalYear,
                        PeriodEndDate = period.PeriodEndDate,
                        Revenue = period.Revenue,
                        OperatingProfit = period.OperatingProfit,
                        NetProfit = period.NetProfit,
                        Eps = period.Eps,
                        NetProfitAttributableToMinorityInterest = period.NetProfitAttributableToMinorityInterest,
                        OtherEquity = period.OtherEquity,
                        TotalEquity = period.TotalEquity,
                        OperatingCashFlow = cfo,
                        Capex = capex,
                        FreeCashFlow = fcf,
                        NetCashFlow = period.NetCashFlow,
                        BookValue = period.BookValuePerShare,
                        ConsolidationType = period.ConsolidationType ?? "consolidated",
                        Source = "IndianAPI",
                        LastSyncedAt = now,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    if (i == 0)
                    {
                        ApplyIndianApiRatiosToEntity(newEntity, data, matchingBs);
                    }

                    await _financialRepository.AddAsync(newEntity);
                }
            }

            await _financialRepository.SaveChangesAsync();
            return true;
        }

        private static void ApplyIndianApiRatiosToEntity(
            StockFinancial entity,
            IndianApiStockOverviewDto data,
            StockBalanceSheet? matchingBalanceSheet)
        {
            if (data.CurrentPrice.HasValue) entity.CurrentPrice = data.CurrentPrice;
            if (data.TtmEps.HasValue) entity.TtmEps = data.TtmEps;
            if (data.PeRatio.HasValue) entity.PeRatio = data.PeRatio;
            if (data.PbRatio.HasValue) entity.PbRatio = data.PbRatio;
            if (data.Roe.HasValue) entity.Roe = data.Roe;
            if (data.Roce.HasValue) entity.Roce = data.Roce;
            if (data.DividendYield.HasValue) entity.DividendYield = data.DividendYield;
            if (data.YearHigh.HasValue) entity.Week52High = data.YearHigh;
            if (data.YearLow.HasValue) entity.Week52Low = data.YearLow;
            if (data.FaceValue.HasValue) entity.FaceValue = data.FaceValue;
            if (data.BookValue.HasValue) entity.BookValue = data.BookValue;
            if (data.SectorPe.HasValue)
            {
                entity.SectorPe = data.SectorPe;
                entity.SectorPeSector = data.SectorName;
            }

            decimal? equityCapital = matchingBalanceSheet?.EquityCapital ?? entity.EquityCapital;
            decimal? faceValue = entity.FaceValue;
            decimal? currentPrice = entity.CurrentPrice;

            var (marketCap, totalShares, eqCap, mcSource) = CalculateMarketCap(equityCapital, faceValue, currentPrice, data.MarketCap);
            if (marketCap.HasValue)
            {
                entity.MarketCap = marketCap;
                entity.MarketCapSource = mcSource;
                entity.TotalShares = totalShares;
                entity.EquityCapital = eqCap;
            }
        }

        private async Task SyncFromBharatStockAsync(Stock stock, CancellationToken cancellationToken)
        {
            IReadOnlyList<BharatStockFinancialRecord>? records = null;
            BharatStockRatiosRecord? ratios = null;
            BharatStockCompanyDetailsRecord? details = null;
            BharatStockScreenerRecord? screener = null;
            decimal? livePrice = null;

            try
            {
                var recordsTask = _financialProvider.GetFinancialsAsync(stock.Symbol, "annual", 1, 3, cancellationToken);
                var ratiosTask = _financialProvider.GetRatiosAsync(stock.Symbol, cancellationToken);
                var detailsTask = _financialProvider.GetStockDetailsAsync(stock.Symbol, stock.Exchange, cancellationToken);
                var screenerTask = _financialProvider.GetScreenerDataAsync(stock.Symbol, stock.Exchange, cancellationToken);
                var livePriceTask = GetLiveQuoteInternalAsync(stock.Symbol, stock.Exchange, cancellationToken);

                await Task.WhenAll(recordsTask, ratiosTask, detailsTask, screenerTask, livePriceTask);
                records = await recordsTask;
                ratios = await ratiosTask;
                details = await detailsTask;
                screener = await screenerTask;
                var liveQuote = await livePriceTask;
                livePrice = liveQuote?.Price;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve financials or ratios from backup provider for stock {Symbol}.", stock.Symbol);
            }

            if (records == null || records.Count == 0)
            {
                _logger.LogWarning("No financial records returned by BharatStock for {Symbol}.", stock.Symbol);
                return;
            }

            // Derive sector valuation
            SectorValuationResultDto? sectorValuation = null;
            var sector = details?.Sector ?? screener?.Sector;
            if (!string.IsNullOrWhiteSpace(sector))
            {
                try
                {
                    sectorValuation = await _sectorValuationService.GetSectorValuationAsync(sector, stock.Exchange, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch sector valuation for {Sector}.", sector);
                }
            }

            var now = DateTime.UtcNow;
            var balanceSheets = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 5);

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
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

                var matchingBs = balanceSheets.FirstOrDefault(b =>
                    (!string.IsNullOrEmpty(b.PeriodKey) && !string.IsNullOrEmpty(record.ResolvedPeriodKey) && b.PeriodKey.Equals(record.ResolvedPeriodKey, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(b.FiscalYear) && !string.IsNullOrEmpty(record.ResolvedFiscalYear) && b.FiscalYear.Equals(record.ResolvedFiscalYear, StringComparison.OrdinalIgnoreCase)) ||
                    (b.PeriodEndDate.HasValue && record.ResolvedPeriodEndDate.HasValue && b.PeriodEndDate.Value.Date == record.ResolvedPeriodEndDate.Value.Date))
                    ?? (i == 0 ? balanceSheets.FirstOrDefault() : null);

                var totalEquityInfo = DetermineTotalEquity(record, matchingBs);

                var existing = await _financialRepository.GetByStockIdAndPeriodKeyAsync(stock.Id, periodKey);
                if (existing != null)
                {
                    existing.FiscalYear = record.ResolvedFiscalYear;
                    existing.PeriodType = record.PeriodType ?? "annual";
                    existing.PeriodEndDate = record.ResolvedPeriodEndDate ?? existing.PeriodEndDate;

                    if (record.ResolvedRevenueFromOperations.HasValue) existing.Revenue = record.ResolvedRevenueFromOperations;
                    if (record.ResolvedOperatingProfitEbitda.HasValue) existing.OperatingProfit = record.ResolvedOperatingProfitEbitda;
                    if (record.NetProfit.HasValue) existing.NetProfit = record.NetProfit;
                    if (record.Eps.HasValue) existing.Eps = record.Eps;
                    if (record.NetProfitAttributableToMinorityInterest.HasValue)
                        existing.NetProfitAttributableToMinorityInterest = record.NetProfitAttributableToMinorityInterest;
                    if (record.OtherEquity.HasValue) existing.OtherEquity = record.OtherEquity;
                    if (cfo.HasValue) existing.OperatingCashFlow = cfo;
                    if (capex.HasValue) existing.Capex = capex;
                    if (fcf.HasValue) existing.FreeCashFlow = fcf;
                    if (record.ResolvedNetCashFlow.HasValue) existing.NetCashFlow = record.ResolvedNetCashFlow;
                    if (!string.IsNullOrWhiteSpace(record.ConsolidationType)) existing.ConsolidationType = record.ConsolidationType;

                    if (totalEquityInfo.TotalEquity.HasValue)
                    {
                        existing.TotalEquity = totalEquityInfo.TotalEquity.Value;
                    }

                    if (i == 0)
                    {
                        ApplyRatiosToEntity(existing, ratios, details, screener, sectorValuation, totalEquityInfo, matchingBs, livePrice);
                    }

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
                        Revenue = record.ResolvedRevenueFromOperations,
                        OperatingProfit = record.ResolvedOperatingProfitEbitda,
                        NetProfit = record.NetProfit,
                        Eps = record.Eps,
                        NetProfitAttributableToMinorityInterest = record.NetProfitAttributableToMinorityInterest,
                        OtherEquity = record.OtherEquity,
                        TotalEquity = totalEquityInfo.TotalEquity,
                        TotalEquitySource = totalEquityInfo.Source,
                        OperatingCashFlow = cfo,
                        Capex = capex,
                        FreeCashFlow = fcf,
                        NetCashFlow = record.ResolvedNetCashFlow,
                        ConsolidationType = record.ConsolidationType ?? "consolidated",
                        Source = !string.IsNullOrWhiteSpace(record.Source) ? record.Source : "BharatStock",
                        LastSyncedAt = now,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    if (i == 0)
                    {
                        ApplyRatiosToEntity(newEntity, ratios, details, screener, sectorValuation, totalEquityInfo, matchingBs, livePrice);
                    }

                    await _financialRepository.AddAsync(newEntity);
                }
            }

            await _financialRepository.SaveChangesAsync();
        }

        private static void ApplyRatiosToEntity(
            StockFinancial entity,
            BharatStockRatiosRecord? ratios,
            BharatStockCompanyDetailsRecord? details = null,
            BharatStockScreenerRecord? screener = null,
            SectorValuationResultDto? sectorValuation = null,
            (decimal? TotalEquity, string? Source, string? Period)? totalEquityInfo = null,
            StockBalanceSheet? matchingBalanceSheet = null,
            decimal? livePrice = null)
        {
            if (ratios != null)
            {
                if (ratios.Roe.HasValue) entity.Roe = ratios.Roe;
                if (ratios.Roce.HasValue) entity.Roce = ratios.Roce;
                if (ratios.PeRatio.HasValue) entity.PeRatio = ratios.PeRatio;
                if (ratios.PbRatio.HasValue) entity.PbRatio = ratios.PbRatio;
                if (ratios.DividendYield.HasValue) entity.DividendYield = ratios.DividendYield;
                if (ratios.Week52High.HasValue) entity.Week52High = ratios.Week52High;
                if (ratios.Week52Low.HasValue) entity.Week52Low = ratios.Week52Low;
                if (ratios.Price.HasValue) entity.CurrentPrice = ratios.Price;
                if (!string.IsNullOrWhiteSpace(ratios.AsOfDate)) entity.RatiosAsOfDate = ratios.AsOfDate;
            }

            if (details != null)
            {
                if (details.FaceValue.HasValue) entity.FaceValue = details.FaceValue;
                if (!entity.CurrentPrice.HasValue && details.LatestPrice?.Close.HasValue == true)
                    entity.CurrentPrice = details.LatestPrice.Close;
            }

            if (livePrice.HasValue)
            {
                entity.CurrentPrice = livePrice.Value;
            }

            if (screener != null)
            {
                if (screener.ResolvedBookValue.HasValue) entity.BookValue = screener.ResolvedBookValue;
                if (screener.MarketCap.HasValue) entity.MarketCap = screener.MarketCap;
                if (!entity.Roe.HasValue && screener.Roe.HasValue) entity.Roe = screener.Roe;
                if (!entity.Roce.HasValue && screener.Roce.HasValue) entity.Roce = screener.Roce;
                if (!entity.PeRatio.HasValue && screener.PeRatio.HasValue) entity.PeRatio = screener.PeRatio;
                if (!entity.PbRatio.HasValue && screener.PbRatio.HasValue) entity.PbRatio = screener.PbRatio;
            }

            if (totalEquityInfo.HasValue)
            {
                if (totalEquityInfo.Value.TotalEquity.HasValue)
                {
                    entity.TotalEquity = totalEquityInfo.Value.TotalEquity.Value;
                }
                entity.TotalEquitySource = totalEquityInfo.Value.Source;
            }

            if (sectorValuation != null)
            {
                if (sectorValuation.SectorPe.HasValue)
                {
                    entity.SectorPe = sectorValuation.SectorPe.Value;
                }
                entity.SectorPeSector = sectorValuation.Sector;
            }

            // Market Cap Manual Calculation:
            // Total Num of Shares = Equity Capital / Face Value
            // Market Cap = Total Num of Shares * Current share price
            decimal? equityCapital = matchingBalanceSheet?.EquityCapital ?? entity.EquityCapital;
            var (marketCap, totalShares, eqCap, mcSource) = CalculateMarketCap(equityCapital, entity.FaceValue, entity.CurrentPrice, entity.MarketCap);
            if (marketCap.HasValue)
            {
                entity.MarketCap = marketCap;
                entity.MarketCapSource = mcSource;
                entity.TotalShares = totalShares;
                entity.EquityCapital = eqCap;
            }
        }

        private static readonly ConcurrentDictionary<string, (YahooLiveQuoteDto Quote, DateTime FetchedAt)> LiveQuoteMemoryCache = new();

        private async Task<YahooLiveQuoteDto?> GetLiveQuoteInternalAsync(string symbol, string? exchange, CancellationToken cancellationToken)
        {
            var cleanSymbol = symbol.Trim().ToUpperInvariant();
            var cleanExchange = string.IsNullOrWhiteSpace(exchange) ? "NSE" : exchange.Trim().ToUpperInvariant();
            var cacheKey = $"{cleanSymbol}:{cleanExchange}";

            if (LiveQuoteMemoryCache.TryGetValue(cacheKey, out var cached) && (DateTime.UtcNow - cached.FetchedAt).TotalSeconds < 30)
            {
                return cached.Quote;
            }

            // 1. Real-time Live Quote from Yahoo Finance (Direct exchange feed NSE/BSE)
            if (_yahooFinanceClient != null)
            {
                try
                {
                    var quote = await _yahooFinanceClient.GetLiveQuoteAsync(cleanSymbol, cleanExchange, cancellationToken);
                    if (quote?.Price.HasValue == true && quote.Price.Value > 0)
                    {
                        LiveQuoteMemoryCache[cacheKey] = (quote, DateTime.UtcNow);
                        return quote;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get live quote from Yahoo Finance for {Symbol}", cleanSymbol);
                }
            }

            // 2. Primary: Try IndianAPI GetCurrentPriceAsync
            if (_indianApiClient != null)
            {
                try
                {
                    var p = await _indianApiClient.GetCurrentPriceAsync(cleanSymbol, cleanExchange, cancellationToken);
                    if (p.HasValue && p.Value > 0)
                    {
                        var q = new YahooLiveQuoteDto
                        {
                            Symbol = cleanSymbol,
                            Price = p.Value
                        };
                        LiveQuoteMemoryCache[cacheKey] = (q, DateTime.UtcNow);
                        return q;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get current price from IndianAPI for {Symbol}", cleanSymbol);
                }
            }

            return null;
        }

        private static (decimal? MarketCap, decimal? TotalShares, decimal? EquityCapital, string? Source) CalculateMarketCap(
            decimal? equityCapital,
            decimal? faceValue,
            decimal? currentPrice,
            decimal? fallbackMarketCap = null)
        {
            if (equityCapital.HasValue && equityCapital.Value > 0 && faceValue.HasValue && faceValue.Value > 0)
            {
                // Formula: Total Num of Shares = Equity Capital (in ₹ Cr) / Face Value (in ₹)
                var totalShares = Math.Round(equityCapital.Value / faceValue.Value, 4); // in Crores of shares

                if (currentPrice.HasValue && currentPrice.Value > 0)
                {
                    // Formula: Market Cap = Total Num of Shares (in Cr) * Current Share Price (in ₹)
                    var marketCap = Math.Round(totalShares * currentPrice.Value, 2); // in ₹ Crores
                    return (marketCap, totalShares, equityCapital, "Calculated (Equity Capital ÷ Face Value × Current Price)");
                }

                return (fallbackMarketCap, totalShares, equityCapital, fallbackMarketCap.HasValue ? "Reported" : null);
            }

            return (fallbackMarketCap, null, equityCapital, fallbackMarketCap.HasValue ? "Reported" : null);
        }

        private static (decimal? TotalEquity, string? Source, string? Period) DetermineTotalEquity(
            BharatStockFinancialRecord? record,
            StockBalanceSheet? matchingBalanceSheet = null)
        {
            // Priority 1: IndianAPI Balance Sheet formula: Total Equity = Total Assets - (Borrowings + Other Liabilities)
            if (matchingBalanceSheet?.CalculatedTotalEquity.HasValue == true)
            {
                return (matchingBalanceSheet.CalculatedTotalEquity.Value, "Calculated (Total Assets - (Borrowings + Other Liabilities))", matchingBalanceSheet.FiscalYear);
            }

            if (matchingBalanceSheet?.TotalAssets.HasValue == true && matchingBalanceSheet?.TotalLiabilities.HasValue == true)
            {
                var calcEquity = matchingBalanceSheet.TotalAssets.Value - matchingBalanceSheet.TotalLiabilities.Value;
                return (calcEquity, "Calculated (Total Assets - Total Liabilities)", matchingBalanceSheet.FiscalYear);
            }

            // Priority 2: Direct reported shareholders' equity / total equity field
            if (record?.TotalEquity.HasValue == true)
            {
                return (record.TotalEquity.Value, "Reported", record.ResolvedFiscalYear);
            }

            if (record?.ShareholdersEquity.HasValue == true)
            {
                return (record.ShareholdersEquity.Value, "Reported", record.ResolvedFiscalYear);
            }

            // Priority 3: Fallback from record Total Assets - Total Liabilities
            if (record?.TotalAssets.HasValue == true && record?.TotalLiabilities.HasValue == true)
            {
                var calcEquity = record.TotalAssets.Value - record.TotalLiabilities.Value;
                return (calcEquity, "Calculated (Total Assets - Total Liabilities)", record.ResolvedFiscalYear);
            }

            return (null, null, null);
        }

        private async Task<StockCashflowResponseDto> BuildResponseDtoAsync(
            Stock stock,
            List<StockFinancial> entities,
            CancellationToken cancellationToken = default)
        {
            if (entities.Count == 0)
            {
                throw new KeyNotFoundException($"No financial records found for stock {stock.Symbol}.");
            }

            var current = entities[0];
            StockFinancial? previous = entities.Count > 1 ? entities[1] : null;

            // Derived calculations
            decimal? cfoToOpRatio = null;
            if (current.OperatingCashFlow.HasValue && current.OperatingProfit.HasValue && current.OperatingProfit.Value != 0)
            {
                cfoToOpRatio = Math.Round(current.OperatingCashFlow.Value / current.OperatingProfit.Value, 2);
            }

            decimal? cfoToNpRatio = null;
            if (current.OperatingCashFlow.HasValue && current.NetProfit.HasValue && current.NetProfit.Value != 0)
            {
                cfoToNpRatio = Math.Round(current.OperatingCashFlow.Value / current.NetProfit.Value, 2);
            }

            decimal? fcfMarginPct = null;
            if (current.FreeCashFlow.HasValue && current.Revenue.HasValue && current.Revenue.Value != 0)
            {
                fcfMarginPct = Math.Round((current.FreeCashFlow.Value / current.Revenue.Value) * 100, 2);
            }

            decimal? capexToCfoPct = null;
            if (current.Capex.HasValue && current.OperatingCashFlow.HasValue && current.OperatingCashFlow.Value != 0)
            {
                capexToCfoPct = Math.Round((current.Capex.Value / current.OperatingCashFlow.Value) * 100, 2);
            }

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

            // Total equity metadata
            string? equitySource = current.TotalEquitySource;
            if (string.IsNullOrWhiteSpace(equitySource) && current.TotalEquity.HasValue)
            {
                equitySource = "Reported";
            }
            string? equityPeriod = current.FiscalYear;

            // Prioritize BalanceSheet-based Total Equity if available, otherwise use entity TotalEquity
            decimal? totalEquity = current.TotalEquity;
            var dbBs = await _balanceSheetRepository.GetRecentByStockIdAsync(stock.Id, 1);
            var latestBs = dbBs.FirstOrDefault();
            if (latestBs?.CalculatedTotalEquity.HasValue == true)
            {
                var bsEquityInfo = DetermineTotalEquity(null, latestBs);
                totalEquity = bsEquityInfo.TotalEquity;
                equitySource = bsEquityInfo.Source;
                equityPeriod = bsEquityInfo.Period;
            }
            else if (!totalEquity.HasValue)
            {
                var bsEquityInfo = DetermineTotalEquity(null, latestBs);
                totalEquity = bsEquityInfo.TotalEquity;
                equitySource = bsEquityInfo.Source;
                equityPeriod = bsEquityInfo.Period;
            }

            // Sector P/E metadata
            string? sectorName = current.SectorPeSector ?? stock.Company?.Industry ?? stock.Company?.CompanyName;
            string? sectorAsOfDate = current.RatiosAsOfDate;
            decimal? sectorPe = current.SectorPe;

            // Market Cap Calculation
            decimal? currentPrice = current.CurrentPrice;
            decimal? faceValue = current.FaceValue;
            decimal? equityCapital = latestBs?.EquityCapital ?? current.EquityCapital;
            var (marketCap, totalShares, eqCap, mcSource) = CalculateMarketCap(equityCapital, faceValue, currentPrice, current.MarketCap);

            var ratiosDto = new StockRatiosDto
            {
                Roe = current.Roe,
                Roce = current.Roce,
                PeRatio = current.PeRatio,
                TtmEps = current.TtmEps,
                PbRatio = current.PbRatio,
                DividendYield = current.DividendYield,
                Week52High = current.Week52High,
                Week52Low = current.Week52Low,
                CurrentPrice = currentPrice,
                AsOfDate = current.RatiosAsOfDate,
                FinancialsFiscalYear = current.FiscalYear,
                FinancialsPeriodType = current.PeriodType,
                FaceValue = faceValue,
                EquityCapital = eqCap,
                TotalShares = totalShares,
                TotalEquity = totalEquity,
                TotalEquityPeriod = equityPeriod,
                TotalEquitySource = equitySource,
                BookValue = current.BookValue,
                MarketCap = marketCap,
                MarketCapSource = mcSource,
                SectorPe = sectorPe,
                SectorPeSector = sectorName,
                SectorPeAsOfDate = sectorAsOfDate
            };

            var summary = new CashflowSummaryDto
            {
                FiscalYear = current.FiscalYear,
                PeriodEndDate = current.PeriodEndDate?.ToString("yyyy-MM-dd"),
                OperatingCashFlow = current.OperatingCashFlow,
                Capex = current.Capex,
                FreeCashFlow = current.FreeCashFlow,
                NetCashFlow = current.NetCashFlow,
                Revenue = current.Revenue,
                OperatingProfit = current.OperatingProfit,
                NetProfit = current.NetProfit,
                Eps = current.Eps,
                OtherEquity = current.OtherEquity,
                TotalEquity = totalEquity,
                CfoToOperatingProfitRatio = cfoToOpRatio,
                CfoToNetProfitRatio = cfoToNpRatio,
                FcfMarginPercent = fcfMarginPct,
                CapexToCfoPercent = capexToCfoPct,
                ConsolidationType = current.ConsolidationType,
                Ratios = ratiosDto,
                YoYChange = yoy
            };

            return new StockCashflowResponseDto
            {
                StockId = stock.Id,
                Symbol = stock.Symbol,
                Exchange = stock.Exchange,
                CompanyName = stock.Company?.CompanyName ?? $"{stock.Symbol} Limited",
                LatestFiscalYear = current.FiscalYear,
                DataAsOf = current.PeriodEndDate?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? current.FiscalYear,
                Source = current.Source,
                LastSyncedAt = current.LastSyncedAt,
                Summary = summary,
                Ratios = ratiosDto
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


