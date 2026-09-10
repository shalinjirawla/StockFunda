using AutoMapper;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StockLens_BusinessLayer.MapperProfile;
using StockLens_BusinessLayer.Services;
using StockLens_DataLayer.Entities;
using StockLens_Infrastructure.DataContext;
using StockLens_Infrastructure.ExternalServices.BharatStock;
using StockLens_Infrastructure.ExternalServices.BharatStock.Exceptions;
using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using StockLens_Infrastructure.ExternalServices.IndianApi;
using StockLens_Infrastructure.ExternalServices.IndianApi.Models;
using StockLens_Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StockLens_UnitTests
{
    public class StockShareholdingServiceTests
    {
        private readonly IMapper _mapper;

        public StockShareholdingServiceTests()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAutoMapper(cfg => cfg.AddProfile<MapperProfile>());
            var serviceProvider = services.BuildServiceProvider();
            _mapper = serviceProvider.GetRequiredService<IMapper>();
        }

        private StockLensDataContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<StockLensDataContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new StockLensDataContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public async Task GetShareholdingByStockIdAsync_WhenDbHasDataAndNotForceRefresh_ShouldServeFromDb()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryDbContext(dbName);

            var stock = new Stock { Id = 10, Symbol = "RELIANCE", Company = new Company { CompanyName = "Reliance Industries Limited", Symbol = "RELIANCE" }, Exchange = "NSE" };
            context.Stocks.Add(stock);

            var s1 = new StockShareholding
            {
                StockId = 10,
                PeriodKey = "2026-06-30",
                Period = "Jun 2026",
                PeriodDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                PromoterHolding = 50.48m,
                FiiHolding = 17.19m,
                DiiHolding = 21.10m,
                GovernmentHolding = 0.17m,
                PublicHolding = 11.05m,
                ShareholdersCount = 4651863,
                Source = "IndianAPI",
                LastSyncedAt = DateTime.UtcNow
            };
            var s2 = new StockShareholding
            {
                StockId = 10,
                PeriodKey = "2026-03-31",
                Period = "Mar 2026",
                PeriodDate = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                PromoterHolding = 50.00m,
                FiiHolding = 18.67m,
                DiiHolding = 20.46m,
                GovernmentHolding = 0.17m,
                PublicHolding = 10.70m,
                ShareholdersCount = 4421289,
                Source = "IndianAPI",
                LastSyncedAt = DateTime.UtcNow
            };
            context.StockShareholdings.AddRange(s1, s2);
            await context.SaveChangesAsync();

            var stockRepo = new StockRepository(context);
            var shareholdingRepo = new StockShareholdingRepository(context);
            var companyRepo = new CompanyRepository(context);
            var indianApiMock = new Mock<IIndianApiShareholdingClient>();
            var bharatStockMock = new Mock<IShareholdingProvider>();

            var service = new StockShareholdingService(
                stockRepo, shareholdingRepo, companyRepo, indianApiMock.Object, bharatStockMock.Object, _mapper, NullLogger<StockShareholdingService>.Instance);

            var result = await service.GetShareholdingByStockIdAsync(10, forceRefresh: false);

            result.Should().NotBeNull();
            result.Symbol.Should().Be("RELIANCE");
            result.CurrentPeriod.Period.Should().Be("Jun 2026");
            result.PreviousPeriod.Should().NotBeNull();
            result.PreviousPeriod!.Period.Should().Be("Mar 2026");

            // Percentage-point change validation: 50.48 - 50.00 = +0.48 pp
            result.Change.Promoter.Should().Be(0.48m);
            result.Change.Fii.Should().Be(-1.48m);
            result.Change.Dii.Should().Be(0.64m);
            result.Change.Public.Should().Be(0.35m);

            // Relative percentage change: (50.48 - 50.00) / 50.00 * 100 = +0.96%
            result.RelativeChange.Promoter.Should().Be(0.96m);

            // Providers should NOT have been called when served from DB cache
            indianApiMock.Verify(p => p.GetQuarterlyShareholdingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            bharatStockMock.Verify(p => p.GetShareholdingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetShareholdingByStockIdAsync_WhenDbEmpty_ShouldFetchFromIndianApiAndUpsert()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryDbContext(dbName);

            var stock = new Stock { Id = 20, Symbol = "TCS", Company = new Company { CompanyName = "Tata Consultancy Services Limited", Symbol = "TCS" }, Exchange = "NSE" };
            context.Stocks.Add(stock);
            await context.SaveChangesAsync();

            var stockRepo = new StockRepository(context);
            var shareholdingRepo = new StockShareholdingRepository(context);
            var companyRepo = new CompanyRepository(context);
            var indianApiMock = new Mock<IIndianApiShareholdingClient>();
            var bharatStockMock = new Mock<IShareholdingProvider>();

            var mockRecords = new List<IndianApiNormalizedQuarterRecord>
            {
                new()
                {
                    Period = "Jun 2026",
                    PeriodKey = "2026-06-30",
                    PeriodDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                    PromoterHolding = 71.77m,
                    FiiHolding = 9.07m,
                    DiiHolding = 13.41m,
                    GovernmentHolding = 0.06m,
                    PublicHolding = 5.69m,
                    ShareholdersCount = 2605182,
                    Source = "IndianAPI"
                },
                new()
                {
                    Period = "Mar 2026",
                    PeriodKey = "2026-03-31",
                    PeriodDate = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                    PromoterHolding = 71.77m,
                    FiiHolding = 9.66m,
                    DiiHolding = 13.34m,
                    GovernmentHolding = 0.06m,
                    PublicHolding = 5.16m,
                    ShareholdersCount = 2450090,
                    Source = "IndianAPI"
                }
            };

            indianApiMock.Setup(p => p.GetQuarterlyShareholdingAsync("TCS", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            var service = new StockShareholdingService(
                stockRepo, shareholdingRepo, companyRepo, indianApiMock.Object, bharatStockMock.Object, _mapper, NullLogger<StockShareholdingService>.Instance);

            var result = await service.GetShareholdingByStockIdAsync(20, forceRefresh: false);

            result.Should().NotBeNull();
            result.CurrentPeriod.Promoter.Should().Be(71.77m);
            result.CurrentPeriod.Government.Should().Be(0.06m);
            result.CurrentPeriod.ShareholdersCount.Should().Be(2605182);
            result.History.Should().HaveCount(2);
            result.Source.Should().Be("IndianAPI");

            // Verify persisted in DB
            var dbRecords = await context.StockShareholdings.Where(s => s.StockId == 20).ToListAsync();
            dbRecords.Should().HaveCount(2);
            dbRecords.Select(r => r.PeriodKey).Should().Contain("2026-06-30");
            dbRecords.All(r => r.Source == "IndianAPI").Should().BeTrue();
        }

        [Fact]
        public async Task GetShareholdingByStockIdAsync_WhenStockHasNoPromoters_ShouldMapNullWithoutFallback()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryDbContext(dbName);

            var stock = new Stock { Id = 50, Symbol = "HDFCBANK", Company = new Company { CompanyName = "HDFC Bank Limited", Symbol = "HDFCBANK" }, Exchange = "NSE" };
            context.Stocks.Add(stock);
            await context.SaveChangesAsync();

            var stockRepo = new StockRepository(context);
            var shareholdingRepo = new StockShareholdingRepository(context);
            var companyRepo = new CompanyRepository(context);
            var indianApiMock = new Mock<IIndianApiShareholdingClient>();
            var bharatStockMock = new Mock<IShareholdingProvider>();

            var mockRecords = new List<IndianApiNormalizedQuarterRecord>
            {
                new()
                {
                    Period = "Jun 2026",
                    PeriodKey = "2026-06-30",
                    PeriodDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                    PromoterHolding = null, // Promoters omitted by IndianAPI
                    FiiHolding = 41.82m,
                    DiiHolding = 41.75m,
                    GovernmentHolding = 0.18m,
                    PublicHolding = 16.27m,
                    ShareholdersCount = 4532753,
                    Source = "IndianAPI"
                }
            };

            indianApiMock.Setup(p => p.GetQuarterlyShareholdingAsync("HDFCBANK", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            var service = new StockShareholdingService(
                stockRepo, shareholdingRepo, companyRepo, indianApiMock.Object, bharatStockMock.Object, _mapper, NullLogger<StockShareholdingService>.Instance);

            var result = await service.GetShareholdingByStockIdAsync(50, forceRefresh: false);

            result.Should().NotBeNull();
            result.CurrentPeriod.Promoter.Should().BeNull(); // Preserved as null, NOT converted to 0
            result.CurrentPeriod.Fii.Should().Be(41.82m);
            result.CurrentPeriod.Dii.Should().Be(41.75m);
            result.Source.Should().Be("IndianAPI");

            // BharatStock must NOT be called for valid missing category
            bharatStockMock.Verify(b => b.GetShareholdingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetShareholdingByStockIdAsync_WhenIndianApiThrowsFatalError_ShouldFallbackToBharatStock()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryDbContext(dbName);

            var stock = new Stock { Id = 60, Symbol = "SBIN", Company = new Company { CompanyName = "State Bank of India", Symbol = "SBIN" }, Exchange = "NSE" };
            context.Stocks.Add(stock);
            await context.SaveChangesAsync();

            var stockRepo = new StockRepository(context);
            var shareholdingRepo = new StockShareholdingRepository(context);
            var companyRepo = new CompanyRepository(context);
            var indianApiMock = new Mock<IIndianApiShareholdingClient>();
            var bharatStockMock = new Mock<IShareholdingProvider>();

            // IndianAPI throws 429 rate limit or 404
            indianApiMock.Setup(p => p.GetQuarterlyShareholdingAsync("SBIN", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProviderApiException("Rate limit exceeded", 429));

            var fallbackRecords = new List<BharatStockShareholdingRecord>
            {
                new()
                {
                    Period = "Jun 2026",
                    PeriodDateString = "2026-06-30",
                    PromoterHolding = 57.49m,
                    FiiHolding = 11.02m,
                    DiiHolding = 24.12m,
                    PublicHolding = 7.37m,
                    Source = "BharatStock"
                }
            };

            bharatStockMock.Setup(b => b.GetShareholdingAsync("SBIN", It.IsAny<CancellationToken>()))
                .ReturnsAsync(fallbackRecords);

            var service = new StockShareholdingService(
                stockRepo, shareholdingRepo, companyRepo, indianApiMock.Object, bharatStockMock.Object, _mapper, NullLogger<StockShareholdingService>.Instance);

            var result = await service.GetShareholdingByStockIdAsync(60, forceRefresh: false);

            result.Should().NotBeNull();
            result.CurrentPeriod.Promoter.Should().Be(57.49m);
            result.Source.Should().Be("BharatStock"); // Correctly reflects fallback source

            // Verify persisted in DB with BharatStock source
            var dbRecord = await context.StockShareholdings.FirstOrDefaultAsync(s => s.StockId == 60);
            dbRecord.Should().NotBeNull();
            dbRecord!.Source.Should().Be("BharatStock");
        }

        [Fact]
        public async Task GetShareholdingByStockIdAsync_SafeUpsert_ShouldNeverOverwriteExistingNonNullWithNull()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryDbContext(dbName);

            var stock = new Stock { Id = 70, Symbol = "RELIANCE", Company = new Company { CompanyName = "Reliance Industries Limited", Symbol = "RELIANCE" }, Exchange = "NSE" };
            context.Stocks.Add(stock);

            // DB already has valid FII and DII
            context.StockShareholdings.Add(new StockShareholding
            {
                StockId = 70,
                PeriodKey = "2026-06-30",
                Period = "Jun 2026",
                PeriodDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                PromoterHolding = 50.48m,
                FiiHolding = 17.19m,
                DiiHolding = 21.10m,
                PublicHolding = 11.05m,
                Source = "IndianAPI"
            });
            await context.SaveChangesAsync();

            var stockRepo = new StockRepository(context);
            var shareholdingRepo = new StockShareholdingRepository(context);
            var companyRepo = new CompanyRepository(context);
            var indianApiMock = new Mock<IIndianApiShareholdingClient>();
            var bharatStockMock = new Mock<IShareholdingProvider>();

            // Incoming record has updated promoter but null FII/DII
            var updatedRecords = new List<IndianApiNormalizedQuarterRecord>
            {
                new()
                {
                    Period = "Jun 2026",
                    PeriodKey = "2026-06-30",
                    PeriodDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                    PromoterHolding = 50.50m,
                    FiiHolding = null, // Incoming null
                    DiiHolding = null, // Incoming null
                    PublicHolding = 11.05m,
                    Source = "IndianAPI"
                }
            };

            indianApiMock.Setup(p => p.GetQuarterlyShareholdingAsync("RELIANCE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(updatedRecords);

            var service = new StockShareholdingService(
                stockRepo, shareholdingRepo, companyRepo, indianApiMock.Object, bharatStockMock.Object, _mapper, NullLogger<StockShareholdingService>.Instance);

            var result = await service.GetShareholdingByStockIdAsync(70, forceRefresh: true);

            // Verify existing FII and DII were protected and preserved
            result.CurrentPeriod.Promoter.Should().Be(50.50m);
            result.CurrentPeriod.Fii.Should().Be(17.19m); // Protected
            result.CurrentPeriod.Dii.Should().Be(21.10m); // Protected
        }

        [Fact]
        public async Task GetShareholdingByStockIdAsync_ReconciliationTolerance_ShouldReconcileWithinPointFivePercent()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryDbContext(dbName);

            var stock = new Stock { Id = 80, Symbol = "INFY", Company = new Company { CompanyName = "Infosys Limited", Symbol = "INFY" }, Exchange = "NSE" };
            context.Stocks.Add(stock);

            // Sum = 13.82 + 27.09 + 42.78 + 0.20 + 15.88 + 0.21 = 99.98% (within ±0.50% tolerance)
            context.StockShareholdings.Add(new StockShareholding
            {
                StockId = 80,
                PeriodKey = "2026-06-30",
                Period = "Jun 2026",
                PeriodDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                PromoterHolding = 13.82m,
                FiiHolding = 27.09m,
                DiiHolding = 42.78m,
                GovernmentHolding = 0.20m,
                PublicHolding = 15.88m,
                OtherHolding = 0.21m,
                Source = "IndianAPI"
            });
            await context.SaveChangesAsync();

            var stockRepo = new StockRepository(context);
            var shareholdingRepo = new StockShareholdingRepository(context);
            var companyRepo = new CompanyRepository(context);
            var indianApiMock = new Mock<IIndianApiShareholdingClient>();
            var bharatStockMock = new Mock<IShareholdingProvider>();

            var service = new StockShareholdingService(
                stockRepo, shareholdingRepo, companyRepo, indianApiMock.Object, bharatStockMock.Object, _mapper, NullLogger<StockShareholdingService>.Instance);

            var result = await service.GetShareholdingByStockIdAsync(80, forceRefresh: false);

            result.CurrentPeriod.Total.Should().Be(99.98m);
            result.Validation.Should().NotBeNull();
            result.Validation!.IsValid.Should().BeTrue(); // Reconciled within ±0.50%
        }
    }
}
