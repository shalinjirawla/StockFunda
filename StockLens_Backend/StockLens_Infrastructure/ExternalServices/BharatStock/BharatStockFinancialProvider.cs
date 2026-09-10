using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockLens_Infrastructure.ExternalServices.BharatStock.Exceptions;
using StockLens_Infrastructure.ExternalServices.BharatStock.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.BharatStock
{
    public class BharatStockFinancialProvider : IFinancialProvider
    {
        private readonly HttpClient _httpClient;
        private readonly BharatStockSettings _settings;
        private readonly ILogger<BharatStockFinancialProvider> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        public BharatStockFinancialProvider(
            HttpClient httpClient,
            IOptions<BharatStockSettings> settings,
            ILogger<BharatStockFinancialProvider> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<BharatStockFinancialRecord>> GetFinancialsAsync(
            string ticker,
            string periodType = "annual",
            int page = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ticker))
            {
                throw new ArgumentException("Ticker symbol is required.", nameof(ticker));
            }

            var cleanTicker = ticker.Trim().ToUpperInvariant();
            var cleanPeriodType = string.IsNullOrWhiteSpace(periodType) ? "annual" : periodType.Trim().ToLowerInvariant();
            var endpoint = $"v1/stocks/{Uri.EscapeDataString(cleanTicker)}/financials?period_type={cleanPeriodType}&page={page}&page_size={pageSize}";

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                var cleanApiKey = System.Text.RegularExpressions.Regex.Replace(_settings.ApiKey, @"[^\x20-\x7E]", "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanApiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-API-Key", cleanApiKey);
                }
            }

            _logger.LogInformation("Requesting financials from BharatStock for ticker: {Ticker} via {Endpoint}", cleanTicker, endpoint);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("Timeout occurred while contacting BharatStock API for ticker {Ticker}", cleanTicker);
                throw new ProviderApiException($"BharatStock API request timed out for ticker '{cleanTicker}'.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP network failure contacting BharatStock API for ticker {Ticker}", cleanTicker);
                throw new ProviderApiException($"Network error connecting to BharatStock API: {ex.Message}", innerException: ex);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("BharatStock API returned {StatusCode} Unauthorized for ticker {Ticker}. Response: {ErrorBody}", (int)response.StatusCode, cleanTicker, errorBody);
                    throw new ProviderApiException($"BharatStock API returned 401 Unauthorized: {errorBody}", (int)response.StatusCode);
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("BharatStock returned 404 Not Found for ticker: {Ticker}", cleanTicker);
                    throw new ProviderNotFoundException(cleanTicker, $"Financial data is not available for ticker '{cleanTicker}'.");
                }

                if (response.StatusCode == (HttpStatusCode)429) // Too Many Requests
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta;
                    _logger.LogWarning("BharatStock API rate limit exceeded (HTTP 429) for ticker {Ticker}. RetryAfter: {RetryAfter}", cleanTicker, retryAfter);
                    throw new ProviderRateLimitException($"Rate limit exceeded for BharatStock API.", retryAfter);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("BharatStock API returned error status {StatusCode} for ticker {Ticker}. Response: {ErrorBody}", statusCode, cleanTicker, errorBody);
                    throw new ProviderApiException($"BharatStock API returned status code {statusCode}: {errorBody}", statusCode);
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                return ParseFinancialResponse(json, cleanTicker);
            }
        }

        public static IReadOnlyList<BharatStockFinancialRecord> ParseFinancialResponse(string json, string ticker)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<BharatStockFinancialRecord>();
            }

            var trimmed = json.Trim();

            // 1. Direct JSON Array: [ { fiscal_year: "FY25", ... }, ... ]
            if (trimmed.StartsWith("["))
            {
                var records = JsonSerializer.Deserialize<List<BharatStockFinancialRecord>>(json, JsonOptions);
                return records ?? (IReadOnlyList<BharatStockFinancialRecord>)Array.Empty<BharatStockFinancialRecord>();
            }

            // 2. Wrapped Object or Single Object
            if (trimmed.StartsWith("{"))
            {
                var wrapper = JsonSerializer.Deserialize<BharatStockFinancialApiResponseWrapper>(json, JsonOptions);
                if (wrapper != null)
                {
                    if (wrapper.Financials != null && wrapper.Financials.Count > 0)
                        return wrapper.Financials;

                    if (wrapper.Data != null && wrapper.Data.Count > 0)
                        return wrapper.Data;

                    if (wrapper.Results != null && wrapper.Results.Count > 0)
                        return wrapper.Results;
                }

                // Try parsing as single record object
                var singleRecord = JsonSerializer.Deserialize<BharatStockFinancialRecord>(json, JsonOptions);
                if (singleRecord != null &&
                    (!string.IsNullOrWhiteSpace(singleRecord.FiscalYear) ||
                     singleRecord.Revenue.HasValue ||
                     singleRecord.CashFlowOperating.HasValue ||
                     singleRecord.NetProfit.HasValue))
                {
                    return new List<BharatStockFinancialRecord> { singleRecord };
                }
            }

            return Array.Empty<BharatStockFinancialRecord>();
        }
    }
}
