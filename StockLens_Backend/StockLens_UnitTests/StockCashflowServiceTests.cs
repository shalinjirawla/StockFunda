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
using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using StockLens_Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StockLens_UnitTests
{
    public class StockCashflowServiceTests : IDisposable
    {
        private readonly StockLensDataContext _dbContext;
        private readonly StockRepository _stockRepository;
        private readonly StockFinancialRepository _financialRepository;
        private readonly CompanyRepository _companyRepository;
        private readonly Mock<IFinancialProvider> _mockFinancialProvider;
        private readonly IMapper _mapper;
        private readonly StockCashflowService _service;

        public StockCashflowServiceTests()
        {
            var options = new DbContextOptionsBuilder<StockLensDataContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new StockLensDataContext(options);
            _stockRepository = new StockRepository(_dbContext);
            _financialRepository = new StockFinancialRepository(_dbContext);
            _companyRepository = new CompanyRepository(_dbContext);
            _mockFinancialProvider = new Mock<IFinancialProvider>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAutoMapper(cfg => cfg.AddProfile<MapperProfile>());
            var serviceProvider = services.BuildServiceProvider();
            _mapper = serviceProvider.GetRequiredService<IMapper>();

            _service = new StockCashflowService(
                _stockRepository,
                _financialRepository,
                _companyRepository,
                _mockFinancialProvider.Object,
                _mapper,
                NullLogger<StockCashflowService>.Instance
            );
        }

        public void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_ShouldCalculateFreeCashFlowAndYoYGrowth()
        {
            // Arrange
            var stock = new Stock
            {
                Symbol = "RELIANCE",
                Company = new Company { CompanyName = "Reliance Industries Limited", Symbol = "RELIANCE" },
                Exchange = "NSE"
            };
            await _stockRepository.AddAsync(stock);
            await _stockRepository.SaveChangesAsync();

            var mockRecords = new List<BharatStockFinancialRecord>
            {
                new()
                {
                    PeriodType = "annual",
                    FiscalYear = "FY25",
                    PeriodEndDateString = "2025-03-31",
                    Revenue = 964693.0m,
                    NetProfit = 79020.0m,
                    Eps = 58.60m,
                    OtherEquity = 1057071.0m,
                    CashFlowOperating = 192113.0m,
                    Capex = 128000.0m,
                    NetCashFlow = 15200.0m,
                    ConsolidationType = "consolidated"
                },
                new()
                {
                    PeriodType = "annual",
                    FiscalYear = "FY24",
                    PeriodEndDateString = "2024-03-31",
                    Revenue = 891534.0m,
                    NetProfit = 69621.0m,
                    Eps = 51.40m,
                    OtherEquity = 974100.0m,
                    CashFlowOperating = 176980.0m,
                    Capex = 121500.0m,
                    NetCashFlow = 11400.0m,
                    ConsolidationType = "consolidated"
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("RELIANCE", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("RELIANCE", "NSE", forceRefresh: true);

            // Assert
            result.Should().NotBeNull();
            result.Symbol.Should().Be("RELIANCE");
            result.LatestFiscalYear.Should().Be("FY25");

            // Verify Free Cash Flow (CFO - Capex = 192113 - 128000 = 64113)
            result.Summary.OperatingCashFlow.Should().Be(192113.0m);
            result.Summary.Capex.Should().Be(128000.0m);
            result.Summary.FreeCashFlow.Should().Be(64113.0m);
            result.Summary.NetCashFlow.Should().Be(15200.0m);

            // Verify CFO / Net Profit conversion ratio (192113 / 79020 ≈ 2.43)
            result.Summary.CfoToNetProfitRatio.Should().Be(2.43m);

            // Verify FCF Margin % (64113 / 964693 * 100 ≈ 6.65%)
            result.Summary.FcfMarginPercent.Should().Be(6.65m);

            // Verify YoY Growth: FCF in FY24 = 176980 - 121500 = 55480.
            // Growth = (64113 - 55480) / 55480 * 100 ≈ 15.56%
            result.Summary.YoYChange.FreeCashFlowChange.Should().Be(8633.0m);
            result.Summary.YoYChange.FreeCashFlowGrowth.Should().Be(15.56m);

            // History should have 2 records
            result.History.Should().HaveCount(2);
            result.History[0].FiscalYear.Should().Be("FY25");
            result.History[0].FreeCashFlow.Should().Be(64113.0m);
            result.History[1].FiscalYear.Should().Be("FY24");
            result.History[1].FreeCashFlow.Should().Be(55480.0m);
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_ShouldHandleNullNetCashFlowGracefully()
        {
            // Arrange
            var stock = new Stock { Symbol = "INFY", Company = new Company { CompanyName = "Infosys Limited", Symbol = "INFY" }, Exchange = "NSE" };
            await _stockRepository.AddAsync(stock);
            await _stockRepository.SaveChangesAsync();

            var mockRecords = new List<BharatStockFinancialRecord>
            {
                new()
                {
                    PeriodType = "annual",
                    FiscalYear = "FY25",
                    PeriodEndDateString = "2025-03-31",
                    Revenue = 153670.0m,
                    NetProfit = 26248.0m,
                    CashFlowOperating = 27850.0m,
                    Capex = 3100.0m,
                    NetCashFlow = null, // Net Cashflow not provided by API
                    ConsolidationType = "consolidated"
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("INFY", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("INFY", "NSE", forceRefresh: true);

            // Assert
            result.Should().NotBeNull();
            result.Summary.FreeCashFlow.Should().Be(24750.0m);
            result.Summary.NetCashFlow.Should().BeNull();
            result.Summary.YoYChange.NetCashFlowGrowth.Should().BeNull();
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_ShouldServeFromDbCacheWhenAvailable()
        {
            // Arrange
            var stock = new Stock { Symbol = "TCS", Company = new Company { CompanyName = "TCS Limited", Symbol = "TCS" }, Exchange = "NSE" };
            await _stockRepository.AddAsync(stock);
            await _stockRepository.SaveChangesAsync();

            var mockRecords = new List<BharatStockFinancialRecord>
            {
                new()
                {
                    PeriodType = "annual",
                    FiscalYear = "FY25",
                    PeriodEndDateString = "2025-03-31",
                    Revenue = 240893.0m,
                    NetProfit = 46580.0m,
                    CashFlowOperating = 48920.0m,
                    Capex = 4200.0m
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("TCS", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            // Act: First call syncs and populates DB
            var firstResult = await _service.GetCashflowBySymbolAsync("TCS", "NSE", forceRefresh: true);

            // Second call with forceRefresh = false should serve from DB cache
            var cachedResult = await _service.GetCashflowBySymbolAsync("TCS", "NSE", forceRefresh: false);

            // Assert
            cachedResult.Should().NotBeNull();
            cachedResult.Summary.OperatingCashFlow.Should().Be(48920.0m);
            cachedResult.Summary.FreeCashFlow.Should().Be(44720.0m);

            // Provider should only have been invoked once during the initial forceRefresh
            _mockFinancialProvider.Verify(p => p.GetFinancialsAsync("TCS", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_ShouldReturnAtMostFirstThreeAnnualRecords()
        {
            // Arrange: 5 annual records from provider
            var stock = new Stock { Symbol = "TATAMOTORS", Company = new Company { CompanyName = "Tata Motors Limited", Symbol = "TATAMOTORS" }, Exchange = "NSE" };
            await _stockRepository.AddAsync(stock);
            await _stockRepository.SaveChangesAsync();

            var mockRecords = new List<BharatStockFinancialRecord>
            {
                new() { PeriodType = "annual", FiscalYear = "FY25", PeriodEndDateString = "2025-03-31", Revenue = 400000m, NetProfit = 31000m, CashFlowOperating = 50000m, Capex = 20000m },
                new() { PeriodType = "annual", FiscalYear = "FY24", PeriodEndDateString = "2024-03-31", Revenue = 350000m, NetProfit = 25000m, CashFlowOperating = 45000m, Capex = 18000m },
                new() { PeriodType = "annual", FiscalYear = "FY23", PeriodEndDateString = "2023-03-31", Revenue = 300000m, NetProfit = 20000m, CashFlowOperating = 40000m, Capex = 16000m },
                new() { PeriodType = "annual", FiscalYear = "FY22", PeriodEndDateString = "2022-03-31", Revenue = 250000m, NetProfit = 15000m, CashFlowOperating = 35000m, Capex = 14000m },
                new() { PeriodType = "annual", FiscalYear = "FY21", PeriodEndDateString = "2021-03-31", Revenue = 200000m, NetProfit = 10000m, CashFlowOperating = 30000m, Capex = 12000m }
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("TATAMOTORS", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("TATAMOTORS", "NSE", forceRefresh: true);

            // Assert: Exactly 3 records returned
            result.Should().NotBeNull();
            result.History.Should().HaveCount(3);
            result.History.Select(h => h.FiscalYear).Should().ContainInOrder("FY25", "FY24", "FY23");
        }
    }
}
