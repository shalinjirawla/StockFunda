using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StockLens_BusinessLayer.Services;
using StockLens_Infrastructure.ExternalServices.BharatStock;
using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StockLens_UnitTests
{
    public class SectorValuationServiceTests
    {
        private readonly Mock<IFinancialProvider> _mockFinancialProvider;
        private readonly IMemoryCache _memoryCache;
        private readonly SectorValuationService _service;

        public SectorValuationServiceTests()
        {
            _mockFinancialProvider = new Mock<IFinancialProvider>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _service = new SectorValuationService(
                _mockFinancialProvider.Object,
                _memoryCache,
                NullLogger<SectorValuationService>.Instance
            );
        }

        [Fact]
        public async Task GetSectorValuationAsync_ShouldCalculateCapWeightedSectorPe_AndExcludeLossMakingCompanies()
        {
            // Arrange
            var sector = "Oil Gas & Consumable Fuels";
            var screenerRecords = new List<BharatStockScreenerRecord>
            {
                // Stock 1: Reliance (Cap: 1,900,000 Cr, Net Profit: 69,197 Cr) -> Eligible
                new()
                {
                    Symbol = "RELIANCE",
                    MarketCap = 1900000m,
                    NetProfitTtm = 69197m,
                    ComputedAt = "2026-08-12"
                },
                // Stock 2: ONGC (Cap: 300,000 Cr, Net Profit in raw rupees: 388,290,000,000 -> 38,829 Cr) -> Eligible
                new()
                {
                    Symbol = "ONGC",
                    MarketCap = 300000m,
                    NetProfitTtm = 388290000000m,
                    ComputedAt = "2026-08-12"
                },
                // Stock 3: Loss-making company (Cap: 20,000 Cr, Net Profit: -2,500 Cr) -> MUST BE EXCLUDED
                new()
                {
                    Symbol = "LOSSOIL",
                    MarketCap = 20000m,
                    NetProfitTtm = -2500m,
                    ComputedAt = "2026-08-12"
                },
                // Stock 4: Zero earnings company (Cap: 10,000 Cr, Net Profit: 0 Cr) -> MUST BE EXCLUDED
                new()
                {
                    Symbol = "ZEROOIL",
                    MarketCap = 10000m,
                    NetProfitTtm = 0m,
                    ComputedAt = "2026-08-12"
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetScreenerBySectorAsync(sector, "NSE", 1, 200, It.IsAny<CancellationToken>()))
                .ReturnsAsync(screenerRecords);

            // Act
            var result = await _service.GetSectorValuationAsync(sector, "NSE");

            // Assert
            // Eligible: Reliance (1.9M cap, 69197 profit) + ONGC (0.3M cap, 38829 profit)
            // Total Cap: 2,200,000 Cr
            // Total Profit: 69,197 + 38,829 = 108,026 Cr
            // Sector P/E = 2,200,000 / 108,026 = 20.3655
            result.Should().NotBeNull();
            result.Sector.Should().Be(sector);
            result.EligibleCompaniesCount.Should().Be(2);
            result.ExcludedCompaniesCount.Should().Be(2);
            result.TotalMarketCapCr.Should().Be(2200000m);
            result.TotalNetProfitCr.Should().Be(108026m);
            result.SectorPe.Should().Be(20.3655m);
        }

        [Fact]
        public async Task GetSectorValuationAsync_ShouldServeFromCache_OnSubsequentCalls()
        {
            // Arrange
            var sector = "Information Technology";
            var screenerRecords = new List<BharatStockScreenerRecord>
            {
                new()
                {
                    Symbol = "TCS",
                    MarketCap = 1500000m,
                    NetProfitTtm = 45000m,
                    ComputedAt = "2026-08-12"
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetScreenerBySectorAsync(sector, "NSE", 1, 200, It.IsAny<CancellationToken>()))
                .ReturnsAsync(screenerRecords);

            // Act 1: Initial call populates cache
            var result1 = await _service.GetSectorValuationAsync(sector, "NSE");

            // Act 2: Second call should retrieve from cache
            var result2 = await _service.GetSectorValuationAsync(sector, "NSE");

            // Assert
            result1.SectorPe.Should().Be(33.3333m);
            result2.SectorPe.Should().Be(33.3333m);
            _mockFinancialProvider.Verify(p => p.GetScreenerBySectorAsync(sector, "NSE", 1, 200, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetSectorValuationAsync_WhenAllCompaniesLossMaking_ShouldReturnNullSectorPe()
        {
            // Arrange
            var sector = "Distressed Industry";
            var screenerRecords = new List<BharatStockScreenerRecord>
            {
                new()
                {
                    Symbol = "DIST1",
                    MarketCap = 5000m,
                    NetProfitTtm = -1000m
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetScreenerBySectorAsync(sector, "NSE", 1, 200, It.IsAny<CancellationToken>()))
                .ReturnsAsync(screenerRecords);

            // Act
            var result = await _service.GetSectorValuationAsync(sector, "NSE");

            // Assert
            result.EligibleCompaniesCount.Should().Be(0);
            result.ExcludedCompaniesCount.Should().Be(1);
            result.SectorPe.Should().BeNull();
        }

        [Fact]
        public async Task GetSectorValuationAsync_WhenSectorIsEmptyOrNull_ShouldReturnEmptyResult()
        {
            // Act
            var result = await _service.GetSectorValuationAsync("");

            // Assert
            result.Sector.Should().BeEmpty();
            result.SectorPe.Should().BeNull();
            _mockFinancialProvider.Verify(p => p.GetScreenerBySectorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
