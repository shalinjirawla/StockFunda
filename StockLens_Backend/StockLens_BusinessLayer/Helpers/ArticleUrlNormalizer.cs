using System;

namespace StockLens_BusinessLayer.Helpers
{
    public static class ArticleUrlNormalizer
    {
        private const string LiveMintBaseUrl = "https://www.livemint.com";

        public static string Normalize(string? rawUrl, string? sourceName = null)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return string.Empty;
            }

            var trimmed = rawUrl.Trim();

            // Block unsafe schemes
            if (trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            // Case 1: If already an absolute HTTP/HTTPS URL, keep it unchanged
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) &&
                (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                return absoluteUri.ToString();
            }

            // Case 2: If a domain was returned without protocol (e.g., livemint.com/... or www.livemint.com/...)
            if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("livemint.com", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("business-standard.com", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("economictimes.indiatimes.com", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("moneycontrol.com", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://{trimmed}";
            }

            // Case 3: IndianAPI upstream news provider is LiveMint for all relative news paths
            // Relative paths like "/market/stock-market-news/..." or "/companies/news/..." reside on LiveMint.
            // (sourceName in IndianAPI indicates the syndicated wire credit e.g. Business Standard/PTI/Reuters, while the hosted article page is on LiveMint).
            if (trimmed.StartsWith("/"))
            {
                return $"{LiveMintBaseUrl}{trimmed}";
            }

            // Case 4: Relative path without leading slash
            if (trimmed.Contains("/") || trimmed.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                return $"{LiveMintBaseUrl}/{trimmed}";
            }

            return string.Empty;
        }
    }
}
