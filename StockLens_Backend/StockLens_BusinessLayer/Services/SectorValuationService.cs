using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StockLens_BusinessLayer.Interfaces;
using StockLens_Infrastructure.ExternalServices.BharatStock;
using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_BusinessLayer.Services
{
    public class SectorValuationService : ISectorValuationService
    {
        private readonly IFinancialProvider _financialProvider;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<SectorValuationService> _logger;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

        public SectorValuationService(
            IFinancialProvider financialProvider,
            IMemoryCache memoryCache,
            ILogger<SectorValuationService> logger)
        {
            _financialProvider = financialProvider;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<SectorValuationResultDto> GetSectorValuationAsync(
            string sector,
            string? exchange = "NSE",
            CancellationToken cancellationToken = default)
        {
            var result = new SectorValuationResultDto
            {
                Sector = sector?.Trim() ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(sector))
            {
                _logger.LogWarning("SectorValuation requested with empty or null sector name.");
                return result;
            }

            var cleanSector = sector.Trim();
            var cleanExchange = string.IsNullOrWhiteSpace(exchange) ? "NSE" : exchange.Trim().ToUpperInvariant();
            var cacheKey = $"SectorPE:{cleanSector.ToUpperInvariant()}:{cleanExchange}:{DateTime.UtcNow:yyyy-MM-dd}";

            if (_memoryCache.TryGetValue(cacheKey, out SectorValuationResultDto? cachedResult) && cachedResult != null)
            {
                _logger.LogInformation("Serving cached Sector P/E for sector '{Sector}' ({Exchange}): {SectorPe}x",
                    cleanSector, cleanExchange, cachedResult.SectorPe);
                return cachedResult;
            }

            _logger.LogInformation("Computing aggregate Sector P/E for sector '{Sector}' ({Exchange}) from BharatStock screener.",
                cleanSector, cleanExchange);

            IReadOnlyList<BharatStockScreenerRecord> screenerRecords;
            try
            {
                screenerRecords = await _financialProvider.GetScreenerBySectorAsync(cleanSector, cleanExchange, 1, 200, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve screener records for sector '{Sector}' ({Exchange}) from financial provider.",
                    cleanSector, cleanExchange);
                return result;
            }

            if (screenerRecords == null || screenerRecords.Count == 0)
            {
                _logger.LogWarning("No screener records found for sector '{Sector}' ({Exchange}). Sector P/E set to null.",
                    cleanSector, cleanExchange);
                return result;
            }

            decimal totalMarketCapCr = 0m;
            decimal totalNetProfitCr = 0m;
            int eligibleCount = 0;
            int excludedCount = 0;
            string? latestComputedAt = null;

            foreach (var stock in screenerRecords)
            {
                if (!string.IsNullOrWhiteSpace(stock.ComputedAt) &&
                    (latestComputedAt == null || string.Compare(stock.ComputedAt, latestComputedAt, StringComparison.Ordinal) > 0))
                {
                    latestComputedAt = stock.ComputedAt;
                }

                // 1. Validate Market Cap (must be positive)
                if (!stock.MarketCap.HasValue || stock.MarketCap.Value <= 0m)
                {
                    excludedCount++;
                    continue;
                }

                // 2. Validate TTM Net Profit (must be strictly positive - negative/zero excluded from denominator)
                if (!stock.NetProfitTtm.HasValue || stock.NetProfitTtm.Value <= 0m)
                {
                    excludedCount++;
                    continue;
                }

                var marketCapCr = stock.MarketCap.Value;

                // 3. Unit normalization rule: Convert absolute rupees to ₹ Crores if needed
                // 1 Crore = 10,000,000 (10^7)
                var rawNetProfit = stock.NetProfitTtm.Value;
                var netProfitCr = rawNetProfit >= 10_000_000m ? rawNetProfit / 10_000_000m : rawNetProfit;

                totalMarketCapCr += marketCapCr;
                totalNetProfitCr += netProfitCr;
                eligibleCount++;
            }

            result.EligibleCompaniesCount = eligibleCount;
            result.ExcludedCompaniesCount = excludedCount;
            result.TotalMarketCapCr = totalMarketCapCr;
            result.TotalNetProfitCr = totalNetProfitCr;
            result.AsOfDate = latestComputedAt ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

            if (eligibleCount > 0 && totalNetProfitCr > 0m)
            {
                // Sector P/E = SUM(Market Cap) / SUM(TTM Net Profit)
                result.SectorPe = Math.Round(totalMarketCapCr / totalNetProfitCr, 4);

                _logger.LogInformation("Calculated Sector P/E for '{Sector}': {SectorPe}x (MarketCap: ₹{MarketCap} Cr, NetProfit: ₹{NetProfit} Cr, Eligible: {Eligible}, Excluded: {Excluded})",
                    cleanSector, result.SectorPe, totalMarketCapCr, totalNetProfitCr, eligibleCount, excludedCount);
            }
            else
            {
                _logger.LogWarning("Insufficient positive earnings to compute Sector P/E for sector '{Sector}' (Eligible: {Eligible}, TotalNetProfit: {TotalNetProfit}). Returning null.",
                    cleanSector, eligibleCount, totalNetProfitCr);
                result.SectorPe = null;
            }

            // Cache computed sector P/E result for 24 hours
            _memoryCache.Set(cacheKey, result, CacheDuration);

            return result;
        }
    }
}
