namespace StockLens_UnitTests.Fixtures
{
    public static class BharatStockResponseFixtures
    {
        public const string SingleQuarterResponse = @"
        {
            ""ticker"": ""RELIANCE"",
            ""period"": ""Q1 FY2026"",
            ""period_date"": ""2025-06-30"",
            ""period_type"": ""Quarterly"",
            ""promoter_holding"": 50.25,
            ""fii_holding"": 18.20,
            ""dii_holding"": 16.30,
            ""public_holding"": 15.25
        }";

        public const string MultiQuarterArrayResponse = @"
        [
            {
                ""period"": ""Q1 FY2026"",
                ""period_date"": ""2025-06-30"",
                ""period_type"": ""Quarterly"",
                ""promoter_holding"": 50.25,
                ""fii_holding"": 18.20,
                ""dii_holding"": 16.30,
                ""public_holding"": 15.25
            },
            {
                ""period"": ""Q4 FY2025"",
                ""period_date"": ""2025-03-31"",
                ""period_type"": ""Quarterly"",
                ""promoter_holding"": 49.80,
                ""fii_holding"": 19.10,
                ""dii_holding"": 15.80,
                ""public_holding"": 15.30
            }
        ]";

        public const string WrappedDataResponse = @"
        {
            ""ticker"": ""TCS"",
            ""message"": ""Success"",
            ""data"": [
                {
                    ""period"": ""Q1 FY2026"",
                    ""period_date"": ""2025-06-30"",
                    ""period_type"": ""Quarterly"",
                    ""promoter_holding"": 72.30,
                    ""fii_holding"": 12.45,
                    ""dii_holding"": 10.15,
                    ""public_holding"": 5.10
                }
            ]
        }";

        public const string WrappedHistoryResponse = @"
        {
            ""ticker"": ""INFY"",
            ""history"": [
                {
                    ""quarter"": ""Q1 FY2026"",
                    ""date"": ""2025-06-30"",
                    ""period_type"": ""Quarterly"",
                    ""promoter_holding"": 14.65,
                    ""fii_holding"": 32.80,
                    ""dii_holding"": 36.45,
                    ""public_holding"": 16.10
                }
            ]
        }";

        public const string OfficialNseShareholdingPatternResponse = @"
        {
            ""as_on_date"": ""2026-06-30"",
            ""promoter_pct"": 50.48,
            ""public_pct"": 49.52,
            ""employee_trust_pct"": 0.0,
            ""source"": ""nse_shareholding_pattern""
        }";

        public const string UnparseableInvalidRecord = @"
        [
            {
                ""source"": ""unknown""
            }
        ]";
    }
}
