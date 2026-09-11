using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.IndianApi
{
    public class IndianApiFinancialsClient : IIndianApiFinancialsClient
    {
        private readonly HttpClient _httpClient;
        private readonly IndianApiSettings _settings;
        private readonly ILogger<IndianApiFinancialsClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        public IndianApiFinancialsClient(
            HttpClient httpClient,
            IOptions<IndianApiSettings> settings,
            ILogger<IndianApiFinancialsClient> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            
            _httpClient.BaseAddress = new Uri("https://stock.indianapi.in");
        }

        public async Task<Dictionary<string, Dictionary<string, decimal?>>?> GetBalanceSheetAsync(string symbol, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("Stock symbol is required.", nameof(symbol));
            }

            var cleanStock = symbol.Trim().ToUpperInvariant();
            var endpoint = $"historical_stats?stock_name={Uri.EscapeDataString(cleanStock)}&stats=balancesheet";

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            
            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                var cleanApiKey = System.Text.RegularExpressions.Regex.Replace(_settings.ApiKey, @"[^\x20-\x7E]", "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanApiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", cleanApiKey);
                }
            }

            _logger.LogInformation("Fetching balance sheet from IndianAPI for symbol: {Symbol}", cleanStock);
            
            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("IndianAPI returned status {StatusCode} for symbol {Symbol}", response.StatusCode, symbol);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal?>>>(json, JsonOptions);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching balance sheet for {Symbol} from IndianAPI.", symbol);
                return null;
            }
        }
    }
}
