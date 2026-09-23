using Microsoft.Extensions.Logging;
using StockLens_Infrastructure.ExternalServices.IndianApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace StockLens_Infrastructure.ExternalServices.GoogleNews
{
    public class GoogleNewsClient : IGoogleNewsClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleNewsClient> _logger;

        public GoogleNewsClient(HttpClient httpClient, ILogger<GoogleNewsClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<IReadOnlyList<IndianApiStandardArticle>> GetStockNewsAsync(
            string symbol,
            string? companyName = null,
            CancellationToken cancellationToken = default)
        {
            var articles = new List<IndianApiStandardArticle>();

            try
            {
                var query = Uri.EscapeDataString($"{symbol} share price");
                var rssUrl = $"https://news.google.com/rss/search?q={query}&hl=en-IN&gl=IN&ceid=IN:en";

                _logger.LogInformation("Fetching Google News RSS for {Symbol}: {Url}", symbol, rssUrl);

                var response = await _httpClient.GetAsync(rssUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Google News RSS returned status code {StatusCode} for {Symbol}", response.StatusCode, symbol);
                    return articles; // Return empty list
                }

                var xmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var doc = XDocument.Parse(xmlContent);

                var items = doc.Descendants("item");

                foreach (var item in items)
                {
                    var title = item.Element("title")?.Value?.Trim() ?? string.Empty;
                    var link = item.Element("link")?.Value?.Trim() ?? string.Empty;
                    var pubDateStr = item.Element("pubDate")?.Value?.Trim();
                    var description = item.Element("description")?.Value?.Trim() ?? string.Empty;
                    var source = item.Element("source")?.Value?.Trim() ?? "Google News";
                    var guid = item.Element("guid")?.Value?.Trim() ?? link;

                    DateTime publishedAt = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(pubDateStr) && DateTime.TryParse(pubDateStr, out var parsedDate))
                    {
                        publishedAt = parsedDate.ToUniversalTime();
                    }

                    // Extract actual source from the title if available (Google News appends it often like "Headline - SourceName")
                    var sourceName = source;
                    if (title.Contains(" - "))
                    {
                        var parts = title.Split(" - ");
                        sourceName = parts.Last().Trim();
                        // Clean up title
                        title = string.Join(" - ", parts.Take(parts.Length - 1)).Trim();
                    }

                    var article = new IndianApiStandardArticle
                    {
                        ExternalNewsId = ComputeSha256Hash(guid), // Hash the massive GUID to a 64-char string to fit the 255-char DB limit perfectly for deduplication
                        Title = title,
                        Description = description,
                        Content = link, // Store the full URL in Content to restore it later for the frontend
                        SourceName = sourceName,
                        SourceUrl = link.Length > 400 ? link.Substring(0, 400) : link, // Truncate to 400 chars to avoid SQL Error 1946 (900 byte limit for non-clustered index)
                        ImageUrl = "", // Google RSS doesn't usually provide direct image URLs in a standard way
                        PublishedAt = publishedAt,
                        Category = "Market News"
                    };

                    articles.Add(article);
                }

                // Sirf top 5 latest news fetch karenge
                return articles.OrderByDescending(a => a.PublishedAt).Take(5).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching or parsing Google News RSS for {Symbol}", symbol);
            }

            return articles;
        }

        private static string ComputeSha256Hash(string rawData)
        {
            if (string.IsNullOrEmpty(rawData)) return string.Empty;
            
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
