using FluentAssertions;
using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using System;
using Xunit;

namespace StockLens_UnitTests
{
    public class ShareholdingValidationTests
    {
        [Theory]
        [InlineData("2025-06-30", "2025-06-30")]
        [InlineData("2024-12-31", "2024-12-31")]
        public void ResolvedPeriodDate_WhenValidIsoDate_ShouldParseAccurately(string input, string expectedDate)
        {
            var record = new BharatStockShareholdingRecord
            {
                PeriodDateString = input
            };

            record.ResolvedPeriodDate.Should().NotBeNull();
            record.ResolvedPeriodDate!.Value.ToString("yyyy-MM-dd").Should().Be(expectedDate);
            record.ResolvedPeriodKey.Should().Be(expectedDate);
        }

        [Theory]
        [InlineData("Q1 FY2026", "2026-Q1")]
        [InlineData("Q4 FY2025", "2025-Q4")]
        [InlineData("Q2 2024", "2024-Q2")]
        public void ResolvedPeriodKey_WhenQuarterLabelProvidedWithoutDate_ShouldNormalizeDeterministicKey(string periodLabel, string expectedKey)
        {
            var record = new BharatStockShareholdingRecord
            {
                Period = periodLabel
            };

            record.ResolvedPeriodKey.Should().Be(expectedKey);
        }

        [Fact]
        public void ResolvedPeriodKey_WhenNoDateAndNoPeriod_ShouldReturnEmptyString()
        {
            var record = new BharatStockShareholdingRecord
            {
                PromoterHolding = 50.0m
            };

            record.ResolvedPeriodKey.Should().BeEmpty();
        }

        [Theory]
        [InlineData(50.25, 18.20, 16.30, 15.25, 100.00, true)]
        [InlineData(50.00, 18.00, 16.00, 15.80, 99.80, true)]
        [InlineData(50.10, 18.20, 16.50, 15.40, 100.20, true)]
        [InlineData(50.00, 10.00, 10.00, 10.00, 80.00, false)] // Out of bounds warning
        public void HoldingTotal_ValidationTolerance_ShouldFlagAppropriately(
            decimal p, decimal f, decimal d, decimal pub, decimal expectedTotal, bool isReasonable)
        {
            var sum = p + f + d + pub;
            sum.Should().Be(expectedTotal);

            var passesTolerance = sum >= 98.0m && sum <= 102.0m;
            passesTolerance.Should().Be(isReasonable);
        }
    }
}
