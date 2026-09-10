using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StockLens_Infrastructure.ExternalServices.BharatStock.Exceptions;
using StockLens_Infrastructure.ExternalServices.IndianApi;
using StockLens_Infrastructure.ExternalServices.IndianApi.Models;
using System;
using System.Linq;
using Xunit;

namespace StockLens_UnitTests
{
    public class IndianApiShareholdingClientTests
    {
        [Fact]
        public void ParseIndianApiResponse_MultiCategoryJson_ShouldPivotAllQuartersChronologically()
        {
            var json = @"
            {
                ""Promoters"": {
                    ""Sep 2023"": 50.27,
                    ""Mar 2026"": 50.00,
                    ""Jun 2026"": 50.48
                },
                ""FIIs"": {
                    ""Sep 2023"": 22.60,
                    ""Mar 2026"": 18.67,
                    ""Jun 2026"": 17.19
                },
                ""DIIs"": {
                    ""Sep 2023"": 15.99,
                    ""Mar 2026"": 20.46,
                    ""Jun 2026"": 21.10
                },
                ""Government"": {
                    ""Sep 2023"": 0.17,
                    ""Mar 2026"": 0.17,
                    ""Jun 2026"": 0.17
                },
                ""Public"": {
                    ""Sep 2023"": 10.98,
                    ""Mar 2026"": 10.70,
                    ""Jun 2026"": 11.05
                },
                ""No. of Shareholders"": {
                    ""Sep 2023"": 3698648.0,
                    ""Mar 2026"": 4421289.0,
                    ""Jun 2026"": 4651863.0
                }
            }";

            var records = IndianApiShareholdingClient.ParseIndianApiResponse(json, "RELIANCE", NullLogger.Instance);

            records.Should().NotBeNull();
            records.Should().HaveCount(3);

            // Latest quarter first
            var latest = records[0];
            latest.PeriodKey.Should().Be("2026-06-30");
            latest.Period.Should().Be("Jun 2026");
            latest.PromoterHolding.Should().Be(50.48m);
            latest.FiiHolding.Should().Be(17.19m);
            latest.DiiHolding.Should().Be(21.10m);
            latest.GovernmentHolding.Should().Be(0.17m);
            latest.PublicHolding.Should().Be(11.05m);
            latest.ShareholdersCount.Should().Be(4651863);
            latest.Source.Should().Be("IndianAPI");

            // Middle quarter
            var middle = records[1];
            middle.PeriodKey.Should().Be("2026-03-31");
            middle.Period.Should().Be("Mar 2026");
            middle.PromoterHolding.Should().Be(50.00m);
            middle.ShareholdersCount.Should().Be(4421289);

            // Oldest quarter
            var oldest = records[2];
            oldest.PeriodKey.Should().Be("2023-09-30");
            oldest.Period.Should().Be("Sep 2023");
            oldest.PromoterHolding.Should().Be(50.27m);
            oldest.ShareholdersCount.Should().Be(3698648);
        }

        [Fact]
        public void ParseIndianApiResponse_WhenPromotersCategoryOmitted_ShouldMapToNullWithoutError()
        {
            // Omitted "Promoters" as returned for HDFCBANK
            var json = @"
            {
                ""FIIs"": {
                    ""Jun 2026"": 41.82
                },
                ""DIIs"": {
                    ""Jun 2026"": 41.75
                },
                ""Government"": {
                    ""Jun 2026"": 0.18
                },
                ""Public"": {
                    ""Jun 2026"": 16.27
                },
                ""No. of Shareholders"": {
                    ""Jun 2026"": 4532753.0
                }
            }";

            var records = IndianApiShareholdingClient.ParseIndianApiResponse(json, "HDFCBANK", NullLogger.Instance);

            records.Should().HaveCount(1);
            var record = records[0];
            record.PromoterHolding.Should().BeNull(); // Preserved as null
            record.FiiHolding.Should().Be(41.82m);
            record.DiiHolding.Should().Be(41.75m);
            record.PublicHolding.Should().Be(16.27m);
            record.GovernmentHolding.Should().Be(0.18m);
            record.ShareholdersCount.Should().Be(4532753);
        }

        [Fact]
        public void ParseIndianApiResponse_MissingQuarterInSingleCategory_ShouldAlignCorrectlyWithoutShifting()
        {
            var json = @"
            {
                ""Promoters"": {
                    ""Jun 2026"": 50.00,
                    ""Mar 2026"": 50.00,
                    ""Dec 2025"": 50.00
                },
                ""FIIs"": {
                    ""Jun 2026"": 17.20,
                    ""Dec 2025"": 19.10
                }
            }";

            var records = IndianApiShareholdingClient.ParseIndianApiResponse(json, "TEST", NullLogger.Instance);

            records.Should().HaveCount(3);

            var jun = records.First(r => r.PeriodKey == "2026-06-30");
            jun.FiiHolding.Should().Be(17.20m);

            var mar = records.First(r => r.PeriodKey == "2026-03-31");
            mar.PromoterHolding.Should().Be(50.00m);
            mar.FiiHolding.Should().BeNull(); // Missing in Mar 2026, preserved as null

            var dec = records.First(r => r.PeriodKey == "2025-12-31");
            dec.FiiHolding.Should().Be(19.10m);
        }

        [Fact]
        public void ParseIndianApiResponse_EmptyPayload_ShouldThrowProviderApiExceptionForFallback()
        {
            var action1 = () => IndianApiShareholdingClient.ParseIndianApiResponse("{}", "TEST", NullLogger.Instance);
            action1.Should().Throw<ProviderApiException>().WithMessage("*empty payload*");

            var action2 = () => IndianApiShareholdingClient.ParseIndianApiResponse("", "TEST", NullLogger.Instance);
            action2.Should().Throw<ProviderApiException>().WithMessage("*empty payload*");
        }

        [Theory]
        [InlineData("Jun 2026", "2026-06-30", "Jun 2026")]
        [InlineData("Mar 2026", "2026-03-31", "Mar 2026")]
        [InlineData("Dec 2025", "2025-12-31", "Dec 2025")]
        [InlineData("Sep 2025", "2025-09-30", "Sep 2025")]
        [InlineData("Sept 2025", "2025-09-30", "Sep 2025")]
        [InlineData("Feb 2024", "2024-02-29", "Feb 2024")] // Leap year February
        [InlineData("Feb 2023", "2023-02-28", "Feb 2023")] // Non-leap year February
        [InlineData("2026-06-30", "2026-06-30", "Jun 2026")]
        public void ResolvePeriod_VariousFormats_ShouldNormalizeToPeriodEnd(string input, string expectedKey, string expectedPeriod)
        {
            var (periodKey, periodDate, normalizedPeriod) = IndianApiNormalizedQuarterRecord.ResolvePeriod(input);

            periodKey.Should().Be(expectedKey);
            normalizedPeriod.Should().Be(expectedPeriod);
            periodDate.Should().NotBeNull();
            periodDate!.Value.ToString("yyyy-MM-dd").Should().Be(expectedKey);
        }
    }
}
