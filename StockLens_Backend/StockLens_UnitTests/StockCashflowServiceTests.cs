using AutoMapper;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StockLens_BusinessLayer.Interfaces;
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

using StockLens_Infrastructure.ExternalServices.IndianApi;
using StockLens_Infrastructure.ExternalServices.YahooFinanceApi;

namespace StockLens_UnitTests
{
    public class StockCashflowServiceTests : IDisposable
    {
        private readonly StockLensDataContext _dbContext;
        private readonly StockRepository _stockRepository;
        private readonly StockFinancialRepository _financialRepository;
        private readonly StockBalanceSheetRepository _balanceSheetRepository;
        private readonly CompanyRepository _companyRepository;
        private readonly Mock<IFinancialProvider> _mockFinancialProvider;
        private readonly Mock<ISectorValuationService> _mockSectorValuationService;
        private readonly Mock<IIndianApiBalanceSheetClient> _mockIndianApiClient;
        private readonly Mock<IYahooFinanceClient> _mockYahooFinanceClient;
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
            _balanceSheetRepository = new StockBalanceSheetRepository(_dbContext);
            _companyRepository = new CompanyRepository(_dbContext);
            _mockFinancialProvider = new Mock<IFinancialProvider>();
            _mockSectorValuationService = new Mock<ISectorValuationService>();
            _mockIndianApiClient = new Mock<IIndianApiBalanceSheetClient>();
            _mockYahooFinanceClient = new Mock<IYahooFinanceClient>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAutoMapper(cfg => cfg.AddProfile<MapperProfile>());
            var serviceProvider = services.BuildServiceProvider();
            _mapper = serviceProvider.GetRequiredService<IMapper>();

            _service = new StockCashflowService(
                _stockRepository,
                _financialRepository,
                _companyRepository,
                _balanceSheetRepository,
                _mockFinancialProvider.Object,
                _mockSectorValuationService.Object,
                _mapper,
                NullLogger<StockCashflowService>.Instance,
                _mockIndianApiClient.Object,
                _mockYahooFinanceClient.Object
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
                    FiscalYear = "FY26",
                    PeriodEndDateString = "2026-03-31",
                    Revenue = 1171706.0m,
                    OperatingProfit = 207911.0m,
                    NetProfit = 95754.0m,
                    Eps = 59.69m,
                    OtherEquity = 1120000.0m,
                    CashFlowOperating = 192113.0m,
                    Capex = 122916.0m,
                    NetCashFlow = 39475.0m,
                    ConsolidationType = "consolidated"
                },
                new()
                {
                    PeriodType = "annual",
                    FiscalYear = "FY25",
                    PeriodEndDateString = "2025-03-31",
                    Revenue = 1000122.0m,
                    OperatingProfit = 178650.0m,
                    NetProfit = 79020.0m,
                    Eps = 58.60m,
                    OtherEquity = 1057071.0m,
                    CashFlowOperating = 176980.0m,
                    Capex = 121500.0m,
                    NetCashFlow = 28500.0m,
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
            result.LatestFiscalYear.Should().Be("FY26");

            // Verify Free Cash Flow (CFO - Capex = 192113 - 122916 = 69197)
            result.Summary.OperatingCashFlow.Should().Be(192113.0m);
            result.Summary.Capex.Should().Be(122916.0m);
            result.Summary.FreeCashFlow.Should().Be(69197.0m);
            result.Summary.NetCashFlow.Should().Be(39475.0m);
            result.Summary.OperatingProfit.Should().Be(207911.0m);

            // Verify CFO / Operating Profit (EBITDA) conversion ratio (192113 / 207911 ≈ 0.92)
            result.Summary.CfoToOperatingProfitRatio.Should().Be(0.92m);

            // Verify FCF Margin % on Revenue from Operations (69197 / 1171706 * 100 ≈ 5.91%)
            result.Summary.FcfMarginPercent.Should().Be(5.91m);

            // Verify YoY Growth: FCF in FY25 = 176980 - 121500 = 55480.
            // Growth = (69197 - 55480) / 55480 * 100 ≈ 24.72%
            result.Summary.YoYChange.FreeCashFlowChange.Should().Be(13717.0m);
            result.Summary.YoYChange.FreeCashFlowGrowth.Should().Be(24.72m);
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
        public async Task GetCashflowBySymbolAsync_ShouldCalculateSummaryAndYoYFromAnnualRecords()
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

            // Assert
            result.Should().NotBeNull();
            result.LatestFiscalYear.Should().Be("FY25");
            result.Summary.OperatingCashFlow.Should().Be(50000m);
            result.Summary.FreeCashFlow.Should().Be(30000m);
            result.Summary.YoYChange.OperatingCashFlowGrowth.Should().Be(11.11m);
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_ShouldMapValuationAndProfitabilityRatios()
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
                    Revenue = 1000122.0m,
                    OperatingProfit = 178650.0m,
                    NetProfit = 79020.0m,
                    CashFlowOperating = 176980.0m,
                    Capex = 121500.0m
                }
            };

            var mockRatios = new BharatStockRatiosRecord
            {
                AsOfDate = "2026-08-12",
                Price = 1421.35m,
                PeRatio = 24.3m,
                PbRatio = 2.1m,
                Roe = 8.9m,
                Roce = 10.4m,
                DividendYield = 0.35m,
                Week52High = 1551.0m,
                Week52Low = 1201.6m,
                FinancialsPeriodType = "annual",
                FinancialsFiscalYear = "FY25"
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("RELIANCE", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            _mockFinancialProvider
                .Setup(p => p.GetRatiosAsync("RELIANCE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRatios);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("RELIANCE", "NSE", forceRefresh: true);

            // Assert
            result.Should().NotBeNull();
            result.Ratios.Should().NotBeNull();
            result.Ratios!.Roe.Should().Be(8.9m);
            result.Ratios!.Roce.Should().Be(10.4m);
            result.Ratios!.PeRatio.Should().Be(24.3m);
            result.Ratios!.Week52High.Should().Be(1551.0m);
            result.Ratios!.Week52Low.Should().Be(1201.6m);
            result.Ratios!.CurrentPrice.Should().Be(1421.35m);
            result.Ratios!.AsOfDate.Should().Be("2026-08-12");

            result.Summary.Ratios.Should().NotBeNull();
            result.Summary.Ratios!.Roe.Should().Be(8.9m);
        }

        [Fact]
        public async Task GetRatiosBySymbolAsync_ShouldReturnRatiosDirectly()
        {
            // Arrange
            var mockRatios = new BharatStockRatiosRecord
            {
                AsOfDate = "2026-08-12",
                Price = 1421.35m,
                PeRatio = 24.3m,
                PbRatio = 2.1m,
                Roe = 8.9m,
                Roce = 10.4m,
                DividendYield = 0.35m,
                Week52High = 1551.0m,
                Week52Low = 1201.6m
            };

            _mockFinancialProvider
                .Setup(p => p.GetRatiosAsync("RELIANCE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRatios);

            // Act
            var result = await _service.GetRatiosBySymbolAsync("RELIANCE");

            // Assert
            result.Should().NotBeNull();
            result!.Roe.Should().Be(8.9m);
            result.Roce.Should().Be(10.4m);
            result.PeRatio.Should().Be(24.3m);
            result.Week52High.Should().Be(1551.0m);
            result.Week52Low.Should().Be(1201.6m);
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_ShouldSyncAndReturnFaceValueBookValueAndMarketCap()
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
                    Revenue = 1000122.0m,
                    OperatingProfit = 178650.0m,
                    NetProfit = 79020.0m,
                    Eps = 58.60m,
                    OtherEquity = 1057071.0m,
                    CashFlowOperating = 176980.0m,
                    Capex = 121500.0m,
                    NetCashFlow = 28500.0m,
                    ConsolidationType = "consolidated"
                }
            };

            var mockRatios = new BharatStockRatiosRecord
            {
                AsOfDate = "2026-08-12",
                Price = 1421.35m,
                PeRatio = 24.3m,
                PbRatio = 2.1m,
                Roe = 8.9m,
                Roce = 10.4m,
                Week52High = 1551.0m,
                Week52Low = 1201.6m
            };

            var mockDetails = new BharatStockCompanyDetailsRecord
            {
                Symbol = "RELIANCE",
                CompanyName = "Reliance Industries Limited",
                Sector = "Oil Gas & Consumable Fuels",
                FaceValue = 10.0m
            };

            var mockScreener = new BharatStockScreenerRecord
            {
                Symbol = "RELIANCE",
                BookValuePerShare = 1120.50m,
                MarketCap = 1925400.00m
            };

            var mockSectorValuation = new SectorValuationResultDto
            {
                Sector = "Oil Gas & Consumable Fuels",
                SectorPe = 17.88m,
                TotalMarketCapCr = 2565620m,
                TotalNetProfitCr = 143520m,
                EligibleCompaniesCount = 5,
                ExcludedCompaniesCount = 1
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("RELIANCE", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            _mockFinancialProvider
                .Setup(p => p.GetRatiosAsync("RELIANCE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRatios);

            _mockFinancialProvider
                .Setup(p => p.GetStockDetailsAsync("RELIANCE", "NSE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDetails);

            _mockFinancialProvider
                .Setup(p => p.GetScreenerDataAsync("RELIANCE", "NSE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockScreener);

            _mockSectorValuationService
                .Setup(s => s.GetSectorValuationAsync("Oil Gas & Consumable Fuels", "NSE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockSectorValuation);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("RELIANCE", "NSE", forceRefresh: true);

            // Assert
            result.Should().NotBeNull();
            result.Ratios.Should().NotBeNull();
            result.Ratios!.FaceValue.Should().Be(10.0m);
            result.Ratios!.BookValue.Should().Be(1120.50m);
            result.Ratios!.MarketCap.Should().Be(1925400.00m);
            result.Ratios!.SectorPe.Should().Be(17.88m);
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_TotalEquity_Priority1_ShouldUseReportedEquity()
        {
            // Arrange
            var stock = new Stock
            {
                Symbol = "TCS",
                Company = new Company { CompanyName = "Tata Consultancy Services Limited", Symbol = "TCS" },
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
                    Revenue = 240893.0m,
                    NetProfit = 46580.0m,
                    TotalEquity = 90127.0m, // Priority 1: Reported
                    TotalAssets = 142350.0m,
                    TotalLiabilities = 52223.0m,
                    OtherEquity = 98450.0m
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("TCS", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("TCS", "NSE", forceRefresh: true);

            // Assert
            result.Ratios.Should().NotBeNull();
            result.Ratios!.TotalEquity.Should().Be(90127.0m);
            result.Ratios!.TotalEquitySource.Should().Be("Reported");
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_TotalEquity_Priority2_ShouldCalculateFromAssetsAndLiabilities()
        {
            // Arrange
            var stock = new Stock
            {
                Symbol = "INFY",
                Company = new Company { CompanyName = "Infosys Limited", Symbol = "INFY" },
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
                    Revenue = 153670.0m,
                    NetProfit = 26248.0m,
                    TotalEquity = null, // No direct reported equity
                    TotalAssets = 125800.0m,
                    TotalLiabilities = 39350.0m,
                    OtherEquity = 74200.0m
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("INFY", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("INFY", "NSE", forceRefresh: true);

            // Assert
            // Priority 2: 125800 - 39350 = 86450
            result.Ratios.Should().NotBeNull();
            result.Ratios!.TotalEquity.Should().Be(86450.0m);
            result.Ratios!.TotalEquitySource.Should().Be("Calculated (Total Assets - Total Liabilities)");
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_TotalEquity_Priority3_ShouldReturnNullWhenFieldsMissing_AndNotUseOtherEquity()
        {
            // Arrange
            var stock = new Stock
            {
                Symbol = "WIPRO",
                Company = new Company { CompanyName = "Wipro Limited", Symbol = "WIPRO" },
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
                    Revenue = 89000.0m,
                    NetProfit = 11000.0m,
                    TotalEquity = null,
                    TotalAssets = null, // Missing assets
                    TotalLiabilities = 20000.0m,
                    OtherEquity = 65000.0m // Should NOT be used as TotalEquity!
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("WIPRO", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("WIPRO", "NSE", forceRefresh: true);

            // Assert
            result.Ratios.Should().NotBeNull();
            result.Ratios!.TotalEquity.Should().BeNull();
            result.Ratios!.TotalEquitySource.Should().BeNull();
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_TotalEquity_PriorityBalanceSheet_ShouldCalculateFromTotalAssetsMinusBorrowingsAndLiabilities()
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

            // Add Balance Sheet record with TotalAssets, Borrowings, OtherLiabilities
            // Formula: 2177546 - (402962 + 870554) = 904030
            var bsRecord = new StockBalanceSheet
            {
                StockId = stock.Id,
                PeriodKey = "annual-2026-03-31",
                PeriodType = "annual",
                FiscalYear = "Mar 2026",
                PeriodEndDate = new DateTime(2026, 3, 31),
                TotalAssets = 2177546.0m,
                Borrowings = 402962.0m,
                OtherLiabilities = 870554.0m,
                EquityCapital = 13532.0m,
                Reserves = 890498.0m,
                Source = "IndianAPI",
                LastSyncedAt = DateTime.UtcNow
            };
            await _balanceSheetRepository.AddRangeAsync(new[] { bsRecord });
            await _balanceSheetRepository.SaveChangesAsync();

            var mockRecords = new List<BharatStockFinancialRecord>
            {
                new()
                {
                    PeriodType = "annual",
                    FiscalYear = "Mar 2026",
                    PeriodEndDateString = "2026-03-31",
                    Revenue = 900000.0m,
                    NetProfit = 70000.0m,
                    TotalEquity = null // Rely on Balance Sheet
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("RELIANCE", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("RELIANCE", "NSE", forceRefresh: true);

            // Assert
            // 2177546 - (402962 + 870554) = 904030
            result.Ratios.Should().NotBeNull();
            result.Ratios!.TotalEquity.Should().Be(904030.0m);
            result.Ratios!.TotalEquityPeriod.Should().Be("Mar 2026");
            result.Ratios!.TotalEquitySource.Should().Be("Calculated (Total Assets - (Borrowings + Other Liabilities))");
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_ShouldCalculateMarketCapUsingManualFormula()
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

            // Balance sheet has EquityCapital = 6765.0m
            var bsRecord = new StockBalanceSheet
            {
                StockId = stock.Id,
                PeriodKey = "annual-2025-03-31",
                PeriodType = "annual",
                FiscalYear = "Mar 2025",
                PeriodEndDate = new DateTime(2025, 3, 31),
                EquityCapital = 6765.0m,
                TotalAssets = 1500000.0m,
                Source = "IndianAPI",
                LastSyncedAt = DateTime.UtcNow
            };
            await _balanceSheetRepository.AddRangeAsync(new[] { bsRecord });
            await _balanceSheetRepository.SaveChangesAsync();

            var mockRecords = new List<BharatStockFinancialRecord>
            {
                new()
                {
                    PeriodType = "annual",
                    FiscalYear = "FY25",
                    PeriodEndDateString = "2025-03-31",
                    Revenue = 1000000.0m,
                    NetProfit = 75000.0m
                }
            };

            var mockDetails = new BharatStockCompanyDetailsRecord
            {
                Symbol = "RELIANCE",
                CompanyName = "Reliance Industries Limited",
                FaceValue = 10.0m
            };

            var mockRatios = new BharatStockRatiosRecord
            {
                Price = 2900.0m
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("RELIANCE", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            _mockFinancialProvider
                .Setup(p => p.GetStockDetailsAsync("RELIANCE", "NSE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDetails);

            _mockFinancialProvider
                .Setup(p => p.GetRatiosAsync("RELIANCE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRatios);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("RELIANCE", "NSE", forceRefresh: true);

            // Assert
            // Total Shares = Equity Capital (6765.0) / Face Value (10.0) = 676.50 Cr shares
            // Market Cap = 676.50 * 2900.0 = 1961850.0 Cr
            result.Should().NotBeNull();
            result.Ratios.Should().NotBeNull();
            result.Ratios!.EquityCapital.Should().Be(6765.0m);
            result.Ratios!.FaceValue.Should().Be(10.0m);
            result.Ratios!.TotalShares.Should().Be(676.50m);
            result.Ratios!.CurrentPrice.Should().Be(2900.0m);
            result.Ratios!.MarketCap.Should().Be(1961850.0m);
            result.Ratios!.MarketCapSource.Should().Be("Calculated (Equity Capital ÷ Face Value × Current Price)");
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_ShouldFetchLivePriceFromIndianApiAndCalculateMarketCap()
        {
            // Arrange
            var stock = new Stock
            {
                Symbol = "TCS",
                Company = new Company { CompanyName = "Tata Consultancy Services Limited", Symbol = "TCS" },
                Exchange = "NSE"
            };
            await _stockRepository.AddAsync(stock);
            await _stockRepository.SaveChangesAsync();

            // Equity Capital = 366.0m, Face Value = 1.0m
            var bsRecord = new StockBalanceSheet
            {
                StockId = stock.Id,
                PeriodKey = "annual-2025-03-31",
                PeriodType = "annual",
                FiscalYear = "Mar 2025",
                PeriodEndDate = new DateTime(2025, 3, 31),
                EquityCapital = 366.0m,
                TotalAssets = 150000.0m,
                Source = "IndianAPI",
                LastSyncedAt = DateTime.UtcNow
            };
            await _balanceSheetRepository.AddRangeAsync(new[] { bsRecord });
            await _balanceSheetRepository.SaveChangesAsync();

            var mockRecords = new List<BharatStockFinancialRecord>
            {
                new()
                {
                    PeriodType = "annual",
                    FiscalYear = "FY25",
                    PeriodEndDateString = "2025-03-31",
                    Revenue = 250000.0m,
                    NetProfit = 45000.0m
                }
            };

            var mockDetails = new BharatStockCompanyDetailsRecord
            {
                Symbol = "TCS",
                CompanyName = "Tata Consultancy Services Limited",
                FaceValue = 1.0m
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("TCS", "annual", 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            _mockFinancialProvider
                .Setup(p => p.GetStockDetailsAsync("TCS", "NSE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDetails);

            // YahooFinance returns live price 4100.0m
            _mockYahooFinanceClient
                .Setup(p => p.GetLiveQuoteAsync("TCS", "NSE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new YahooLiveQuoteDto { Symbol = "TCS", Price = 4100.0m });

            // Act
            var result = await _service.GetCashflowBySymbolAsync("TCS", "NSE", forceRefresh: true);

            // Assert
            // Total Shares = 366.0 / 1.0 = 366.0 Cr shares
            // Market Cap = 366.0 * 4100.0 = 1500600.0 Cr
            result.Should().NotBeNull();
            result.Ratios.Should().NotBeNull();
            result.Ratios!.EquityCapital.Should().Be(366.0m);
            result.Ratios!.FaceValue.Should().Be(1.0m);
            result.Ratios!.TotalShares.Should().Be(366.0m);
            result.Ratios!.CurrentPrice.Should().Be(4100.0m);
            result.Ratios!.MarketCap.Should().Be(1500600.0m);
            result.Ratios!.MarketCapSource.Should().Be("Calculated (Equity Capital ÷ Face Value × Current Price)");
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_WhenIndianApiProvidesData_ShouldSyncFromIndianApiAsPrimary()
        {
            // Arrange
            var stock = new Stock
            {
                Symbol = "INFY",
                Company = new Company { CompanyName = "Infosys Limited", Symbol = "INFY" },
                Exchange = "NSE"
            };
            await _stockRepository.AddAsync(stock);
            await _stockRepository.SaveChangesAsync();

            var indianOverview = new IndianApiStockOverviewDto
            {
                Symbol = "INFY",
                CompanyName = "Infosys Limited",
                Industry = "IT Services & Consulting",
                CurrentPrice = 1850.25m,
                YearHigh = 1990.0m,
                YearLow = 1380.0m,
                PeRatio = 26.1m,
                PbRatio = 7.8m,
                Roe = 31.5m,
                Roce = 34.98m,
                DividendYield = 2.10m,
                MarketCap = 768250.0m,
                FaceValue = 5.0m,
                BookValue = 237.40m,
                SectorPe = 27.5m,
                SectorName = "IT Services & Consulting",
                Financials = new List<IndianApiFinancialPeriodDto>
                {
                    new()
                    {
                        FiscalYear = "FY25",
                        PeriodEndDate = new DateTime(2025, 3, 31),
                        PeriodType = "annual",
                        Revenue = 153670.0m,
                        OperatingProfit = 31000.0m,
                        NetProfit = 26248.0m,
                        Eps = 63.40m,
                        OperatingCashFlow = 27850.0m,
                        Capex = 3100.0m,
                        NetCashFlow = 2100.0m,
                        TotalEquity = 86450.0m,
                        BookValuePerShare = 237.40m,
                        TotalShares = 415.2m,
                        EquityCapital = 2076.0m
                    },
                    new()
                    {
                        FiscalYear = "FY24",
                        PeriodEndDate = new DateTime(2024, 3, 31),
                        PeriodType = "annual",
                        Revenue = 146767.0m,
                        OperatingProfit = 28500.0m,
                        NetProfit = 24095.0m,
                        Eps = 58.10m,
                        OperatingCashFlow = 24980.0m,
                        Capex = 2850.0m,
                        NetCashFlow = 1900.0m,
                        TotalEquity = 78200.0m,
                        BookValuePerShare = 210.0m,
                        TotalShares = 415.2m,
                        EquityCapital = 2076.0m
                    }
                }
            };

            _mockIndianApiClient
                .Setup(p => p.GetStockFinancialsAndOverviewAsync("INFY", "NSE", It.IsAny<CancellationToken>()))
                .ReturnsAsync(indianOverview);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("INFY", "NSE", forceRefresh: true);

            // Assert
            result.Should().NotBeNull();
            result.Source.Should().Be("IndianAPI");
            result.LatestFiscalYear.Should().Be("FY25");
            result.Summary.OperatingCashFlow.Should().Be(27850.0m);
            result.Summary.Capex.Should().Be(3100.0m);
            result.Summary.FreeCashFlow.Should().Be(24750.0m); // 27850 - 3100
            result.Summary.Revenue.Should().Be(153670.0m);
            result.Summary.NetProfit.Should().Be(26248.0m);
            result.Summary.Eps.Should().Be(63.40m);
            result.Ratios.Should().NotBeNull();
            result.Ratios!.BookValue.Should().Be(237.40m);
            result.Ratios!.Roe.Should().Be(31.5m);
            result.Ratios!.Roce.Should().Be(34.98m);

            // Verify BharatStock was NOT called
            _mockFinancialProvider.Verify(p => p.GetFinancialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetCashflowBySymbolAsync_WhenIndianApiFails_ShouldFallbackToBharatStock()
        {
            // Arrange
            var stock = new Stock
            {
                Symbol = "WIPRO",
                Company = new Company { CompanyName = "Wipro Limited", Symbol = "WIPRO" },
                Exchange = "NSE"
            };
            await _stockRepository.AddAsync(stock);
            await _stockRepository.SaveChangesAsync();

            // IndianAPI returns null (fails)
            _mockIndianApiClient
                .Setup(p => p.GetStockFinancialsAndOverviewAsync("WIPRO", "NSE", It.IsAny<CancellationToken>()))
                .ReturnsAsync((IndianApiStockOverviewDto?)null);

            var mockRecords = new List<BharatStockFinancialRecord>
            {
                new()
                {
                    PeriodType = "annual",
                    FiscalYear = "FY25",
                    PeriodEndDateString = "2025-03-31",
                    Revenue = 89000.0m,
                    OperatingProfit = 15000.0m,
                    NetProfit = 11000.0m,
                    Eps = 21.50m,
                    CashFlowOperating = 13500.0m,
                    Capex = 2200.0m,
                    NetCashFlow = 1500.0m,
                    Source = "BharatStock"
                }
            };

            _mockFinancialProvider
                .Setup(p => p.GetFinancialsAsync("WIPRO", "annual", 1, 3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRecords);

            // Act
            var result = await _service.GetCashflowBySymbolAsync("WIPRO", "NSE", forceRefresh: true);

            // Assert
            result.Should().NotBeNull();
            result.Source.Should().Be("BharatStock");
            result.LatestFiscalYear.Should().Be("FY25");
            result.Summary.OperatingCashFlow.Should().Be(13500.0m);
            result.Summary.Capex.Should().Be(2200.0m);
            result.Summary.FreeCashFlow.Should().Be(11300.0m);

            // Verify BharatStock was called as fallback
            _mockFinancialProvider.Verify(p => p.GetFinancialsAsync("WIPRO", "annual", 1, 3, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}


