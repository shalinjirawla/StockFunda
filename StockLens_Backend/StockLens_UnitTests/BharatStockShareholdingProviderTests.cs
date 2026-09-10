using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using StockLens_Infrastructure.ExternalServices.BharatStock;
using StockLens_Infrastructure.ExternalServices.BharatStock.Exceptions;
using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using StockLens_UnitTests.Fixtures;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StockLens_UnitTests
{
    public class BharatStockShareholdingProviderTests
    {
        private readonly IOptions<BharatStockSettings> _options;

        public BharatStockShareholdingProviderTests()
        {
            _options = Options.Create(new BharatStockSettings
            {
                BaseUrl = "https://api.bharatstock.in",
                ApiKey = "test-api-key",
                TimeoutSeconds = 5,
                UseMockData = false
            });
        }

        [Fact]
        public void ParseShareholdingResponse_OfficialDocumentationOneRow_ShouldMapAccurately()
        {
            var records = BharatStockShareholdingProvider.ParseShareholdingResponse(
                BharatStockResponseFixtures.OfficialNseShareholdingPatternResponse, "RELIANCE");

            records.Should().HaveCount(1);
            var item = records[0];
            item.PromoterHolding.Should().Be(50.48m);
            item.PublicHolding.Should().Be(49.52m);
            item.Source.Should().Be("nse_shareholding_pattern");
            item.ResolvedPeriodDate.Should().Be(new DateTime(2026, 6, 30));
            item.ResolvedPeriodKey.Should().Be("2026-06-30");
        }

        [Fact]
        public void ParseShareholdingResponse_SingleQuarter_ShouldMapCorrectly()
        {
            var records = BharatStockShareholdingProvider.ParseShareholdingResponse(
                BharatStockResponseFixtures.SingleQuarterResponse, "RELIANCE");

            records.Should().HaveCount(1);
            var item = records[0];
            item.PromoterHolding.Should().Be(50.25m);
            item.FiiHolding.Should().Be(18.20m);
            item.DiiHolding.Should().Be(16.30m);
            item.PublicHolding.Should().Be(15.25m);
            item.ResolvedPeriod.Should().Be("Q1 FY2026");
            item.ResolvedPeriodKey.Should().Be("2025-06-30");
        }

        [Fact]
        public void ParseShareholdingResponse_MultiQuarterArray_ShouldMapAllQuarters()
        {
            var records = BharatStockShareholdingProvider.ParseShareholdingResponse(
                BharatStockResponseFixtures.MultiQuarterArrayResponse, "RELIANCE");

            records.Should().HaveCount(2);
            records[0].ResolvedPeriodKey.Should().Be("2025-06-30");
            records[1].ResolvedPeriodKey.Should().Be("2025-03-31");
            records[0].PromoterHolding.Should().Be(50.25m);
            records[1].PromoterHolding.Should().Be(49.80m);
        }

        [Fact]
        public void ParseShareholdingResponse_WrappedData_ShouldExtractNestedArray()
        {
            var records = BharatStockShareholdingProvider.ParseShareholdingResponse(
                BharatStockResponseFixtures.WrappedDataResponse, "TCS");

            records.Should().HaveCount(1);
            records[0].PromoterHolding.Should().Be(72.30m);
            records[0].ResolvedPeriodKey.Should().Be("2025-06-30");
        }

        [Fact]
        public void ParseShareholdingResponse_WrappedHistory_ShouldExtractNestedHistory()
        {
            var records = BharatStockShareholdingProvider.ParseShareholdingResponse(
                BharatStockResponseFixtures.WrappedHistoryResponse, "INFY");

            records.Should().HaveCount(1);
            records[0].PromoterHolding.Should().Be(14.65m);
            records[0].ResolvedPeriodKey.Should().Be("2025-06-30");
        }

        [Fact]
        public async Task GetShareholdingAsync_WhenApiReturns404_ShouldThrowProviderNotFoundException()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent("{\"message\": \"Stock not found\"}")
                });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://api.bharatstock.in/")
            };

            var provider = new BharatStockShareholdingProvider(
                httpClient, _options, NullLogger<BharatStockShareholdingProvider>.Instance);

            var act = () => provider.GetShareholdingAsync("UNKNOWN_STOCK");

            await act.Should().ThrowAsync<ProviderNotFoundException>()
                .Where(ex => ex.Ticker == "UNKNOWN_STOCK");
        }

        [Fact]
        public async Task GetShareholdingAsync_WhenApiReturns429_ShouldThrowProviderRateLimitException()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            var response = new HttpResponseMessage
            {
                StatusCode = (HttpStatusCode)429,
                Content = new StringContent("{\"message\": \"Rate limit exceeded\"}")
            };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://api.bharatstock.in/")
            };

            var provider = new BharatStockShareholdingProvider(
                httpClient, _options, NullLogger<BharatStockShareholdingProvider>.Instance);

            var act = () => provider.GetShareholdingAsync("RELIANCE");

            var ex = await act.Should().ThrowAsync<ProviderRateLimitException>();
            ex.Which.StatusCode.Should().Be(429);
            ex.Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(60));
        }

        [Fact]
        public async Task GetShareholdingAsync_WhenApiReturns500_ShouldThrowProviderApiException()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("{\"error\": \"Internal server error\"}")
                });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://api.bharatstock.in/")
            };

            var provider = new BharatStockShareholdingProvider(
                httpClient, _options, NullLogger<BharatStockShareholdingProvider>.Instance);

            var act = () => provider.GetShareholdingAsync("RELIANCE");

            var ex = await act.Should().ThrowAsync<ProviderApiException>();
            ex.Which.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task MockShareholdingProvider_ShouldGenerateValidRealisticHistory()
        {
            var mockProvider = new MockShareholdingProvider(NullLogger<MockShareholdingProvider>.Instance);
            var result = await mockProvider.GetShareholdingAsync("RELIANCE");

            result.Should().NotBeNull();
            result.Count.Should().BeGreaterThan(1);
            result[0].PromoterHolding.Should().Be(50.25m);
            result[0].ResolvedPeriodKey.Should().NotBeNullOrWhiteSpace();
        }
    }
}
