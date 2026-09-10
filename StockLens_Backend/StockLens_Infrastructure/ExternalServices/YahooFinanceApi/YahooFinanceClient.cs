using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.YahooFinanceApi
{
    public class YahooFinanceClient : IYahooFinanceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<YahooFinanceClient> _logger;

        public YahooFinanceClient(HttpClient httpClient, ILogger<YahooFinanceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri("https://query2.finance.yahoo.com");
            // Add required headers to prevent blocking
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        }

        public async Task<(string? CompanyName, string? Industry)> GetCompanyDetailsAsync(string symbol, string exchange, CancellationToken cancellationToken = default)
        {
            try
            {
                var suffix = exchange.Equals("BSE", StringComparison.OrdinalIgnoreCase) ? ".BO" : ".NS";
                var searchSymbol = $"{symbol.Trim().ToUpperInvariant()}{suffix}";

                var endpoint = $"/v1/finance/search?q={Uri.EscapeDataString(searchSymbol)}&quotesCount=1";
                
                _logger.LogInformation("Fetching real company name for {Symbol} via Yahoo Finance API", searchSymbol);
                var response = await _httpClient.GetAsync(endpoint, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var document = JsonDocument.Parse(json);
                    
                    if (document.RootElement.TryGetProperty("quotes", out var quotesElement) && 
                        quotesElement.ValueKind == JsonValueKind.Array && 
                        quotesElement.GetArrayLength() > 0)
                    {
                        var firstQuote = quotesElement[0];
                        string? companyName = null;
                        string? industry = null;

                        if (firstQuote.TryGetProperty("longname", out var longNameElement))
                        {
                            companyName = longNameElement.GetString();
                        }
                        else if (firstQuote.TryGetProperty("shortname", out var shortNameElement))
                        {
                            companyName = shortNameElement.GetString();
                        }

                        if (firstQuote.TryGetProperty("industryDisp", out var industryElement) || 
                            firstQuote.TryGetProperty("industry", out industryElement))
                        {
                            industry = industryElement.GetString();
                        }

                        return (companyName, industry);
                    }
                }
                else
                {
                    _logger.LogWarning("Yahoo Finance API returned status code {StatusCode} for {Symbol}", response.StatusCode, searchSymbol);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching company name from Yahoo Finance API for {Symbol}", symbol);
            }

            return (null, null);
        }
    }
}
