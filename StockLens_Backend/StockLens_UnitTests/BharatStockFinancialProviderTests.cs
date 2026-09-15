using FluentAssertions;
using StockLens_Infrastructure.ExternalServices.BharatStock;
using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using System.Linq;
using Xunit;

namespace StockLens_UnitTests
{
    public class BharatStockFinancialProviderTests
    {
        [Fact]
        public void ParseFinancialResponse_ShouldParseSingleRowObject()
        {
            // Exact JSON sample from BharatStock documentation in user prompt
            var json = @"{
              ""period_type"": ""annual"",
              ""fiscal_year"": ""FY25"",
              ""period_end_date"": ""2025-03-31"",
              ""revenue"": 964693.0,
              ""net_profit"": 79020.0,
              ""eps"": 58.6,
              ""net_profit_attributable_to_minority_interest"": 6821.0,
              ""other_equity"": 1057071.0,
              ""cash_flow_operating"": 192113.0,
              ""capex"": 128000.0,
              ""net_cash_flow"": 15200.0,
              ""consolidation_type"": ""consolidated""
            }";

            var records = BharatStockFinancialProvider.ParseFinancialResponse(json, "RELIANCE");

            records.Should().NotBeNull();
            records.Should().HaveCount(1);

            var row = records.First();
            row.FiscalYear.Should().Be("FY25");
            row.ResolvedFiscalYear.Should().Be("FY25");
            row.Revenue.Should().Be(964693.0m);
            row.NetProfit.Should().Be(79020.0m);
            row.Eps.Should().Be(58.6m);
            row.CashFlowOperating.Should().Be(192113.0m);
            row.Capex.Should().Be(128000.0m);
            row.NetCashFlow.Should().Be(15200.0m);
            row.OtherEquity.Should().Be(1057071.0m);
            row.ConsolidationType.Should().Be("consolidated");
        }

        [Fact]
        public void ParseFinancialResponse_ShouldParseArrayOfAnnualStatements()
        {
            var json = @"[
              {
                ""period_type"": ""annual"",
                ""fiscal_year"": ""FY25"",
                ""period_end_date"": ""2025-03-31"",
                ""revenue"": 964693.0,
                ""net_profit"": 79020.0,
                ""eps"": 58.6,
                ""cash_flow_operating"": 192113.0,
                ""capex"": 128000.0,
                ""consolidation_type"": ""consolidated""
              },
              {
                ""period_type"": ""annual"",
                ""fiscal_year"": ""FY24"",
                ""period_end_date"": ""2024-03-31"",
                ""revenue"": 891534.0,
                ""net_profit"": 69621.0,
                ""eps"": 51.4,
                ""cash_flow_operating"": 176980.0,
                ""capex"": 121500.0,
                ""consolidation_type"": ""consolidated""
              }
            ]";

            var records = BharatStockFinancialProvider.ParseFinancialResponse(json, "RELIANCE");

            records.Should().HaveCount(2);
            records[0].ResolvedFiscalYear.Should().Be("FY25");
            records[1].ResolvedFiscalYear.Should().Be("FY24");
        }

        [Fact]
        public void ParseFinancialResponse_ShouldParseWrappedDataResponse()
        {
            var json = @"{
              ""financials"": [
                {
                  ""period_type"": ""annual"",
                  ""fiscal_year"": ""FY25"",
                  ""period_end_date"": ""2025-03-31"",
                  ""revenue"": 240893.0,
                  ""net_profit"": 46580.0,
                  ""eps"": 128.4,
                  ""cash_flow_operating"": 48920.0,
                  ""capex"": 4200.0,
                  ""consolidation_type"": ""consolidated""
                }
              ]
            }";

            var records = BharatStockFinancialProvider.ParseFinancialResponse(json, "TCS");

            records.Should().HaveCount(1);
            records[0].Revenue.Should().Be(240893.0m);
            records[0].CashFlowOperating.Should().Be(48920.0m);
            records[0].Capex.Should().Be(4200.0m);
        }

        [Fact]
        public void ParseCompanyDetailsResponse_ShouldParseFaceValueAndCompanyInfo()
        {
            var json = @"{
              ""symbol"": ""RELIANCE"",
              ""company_name"": ""Reliance Industries Limited"",
              ""sector"": ""Oil Gas & Consumable Fuels"",
              ""exchange"": ""NSE"",
              ""industry"": ""Petroleum Products"",
              ""face_value"": 10.0,
              ""latest_price"": {
                ""trade_date"": ""2026-08-12"",
                ""close"": 1421.35,
                ""prev_close"": 1408.90,
                ""volume"": 8452110,
                ""delivery_pct"": 42.6
              }
            }";

            var result = BharatStockFinancialProvider.ParseCompanyDetailsResponse(json, "RELIANCE");

            result.Should().NotBeNull();
            result!.Symbol.Should().Be("RELIANCE");
            result.CompanyName.Should().Be("Reliance Industries Limited");
            result.FaceValue.Should().Be(10.0m);
            result.LatestPrice.Should().NotBeNull();
            result.LatestPrice!.Close.Should().Be(1421.35m);
        }

        [Fact]
        public void ParseScreenerResponse_ShouldParseBookValueAndMarketCap()
        {
            var json = @"[
              {
                ""symbol"": ""ONGC"",
                ""company_name"": ""Oil and Natural Gas Corporation Limited"",
                ""sector"": ""Oil Gas & Consumable Fuels"",
                ""exchange"": ""NSE"",
                ""price"": 262.50,
                ""market_cap"": 330120.45,
                ""pe_ratio"": 7.8,
                ""pb_ratio"": 1.2,
                ""book_value_per_share"": 218.75,
                ""roe"": 18.5,
                ""roce"": 22.1,
                ""computed_at"": ""2026-08-15""
              }
            ]";

            var result = BharatStockFinancialProvider.ParseScreenerResponse(json, "ONGC");

            result.Should().NotBeNull();
            result!.Symbol.Should().Be("ONGC");
            result.MarketCap.Should().Be(330120.45m);
            result.ResolvedBookValue.Should().Be(218.75m);
            result.PeRatio.Should().Be(7.8m);
        }
    }
}

