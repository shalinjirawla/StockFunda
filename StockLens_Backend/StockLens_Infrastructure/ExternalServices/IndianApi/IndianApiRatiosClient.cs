using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockLens_Infrastructure.ExternalServices.BharatStock.Exceptions;
using StockLens_Infrastructure.ExternalServices.IndianApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.IndianApi
{
    public class IndianApiRatiosClient : IIndianApiRatiosClient
    {
        private readonly HttpClient _httpClient;
        private readonly IndianApiSettings _settings;
        private readonly ILogger<IndianApiRatiosClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        public IndianApiRatiosClient(
            HttpClient httpClient,
            IOptions<IndianApiSettings> settings,
            ILogger<IndianApiRatiosClient> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<IndianApiNormalizedRatioRecord>> GetRatiosAsync(
            string stockName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(stockName))
            {
                throw new ArgumentException("Stock name/symbol is required.", nameof(stockName));
            }

            var cleanStock = stockName.Trim().ToUpperInvariant();
            var endpoint = $"historical_stats?stock_name={Uri.EscapeDataString(cleanStock)}&stats=ratios";

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                var cleanApiKey = Regex.Replace(_settings.ApiKey, @"[^\x20-\x7E]", "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanApiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", cleanApiKey);
                }
            }

            _logger.LogInformation("Fetching historical ratios for {Symbol} from IndianAPI via {Endpoint}", cleanStock, endpoint);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("Timeout occurred while contacting IndianAPI for stock {Symbol}", cleanStock);
                throw new ProviderApiException($"IndianAPI historical stats request timed out for stock '{cleanStock}'.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP network failure contacting IndianAPI for stock {Symbol}", cleanStock);
                throw new ProviderApiException($"Network error connecting to IndianAPI: {ex.Message}", innerException: ex);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("IndianAPI returned {StatusCode} Unauthorized for stock {Symbol}. Response: {ErrorBody}",
                        (int)response.StatusCode, cleanStock, errorBody);
                    throw new ProviderApiException($"IndianAPI returned {(int)response.StatusCode} Unauthorized.", (int)response.StatusCode);
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("IndianAPI returned 404 Not Found for stock {Symbol}", cleanStock);
                    throw new ProviderNotFoundException(cleanStock, $"Historical ratios data not found for stock '{cleanStock}' in IndianAPI.");
                }

                if (response.StatusCode == (HttpStatusCode)429) // Rate limit
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta;
                    _logger.LogWarning("IndianAPI rate limit exceeded (HTTP 429) for stock {Symbol}. RetryAfter: {RetryAfter}", cleanStock, retryAfter);
                    throw new ProviderRateLimitException("Rate limit exceeded for IndianAPI.", retryAfter);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("IndianAPI returned error status {StatusCode} for stock {Symbol}. Response: {ErrorBody}", statusCode, cleanStock, errorBody);
                    throw new ProviderApiException($"IndianAPI returned status code {statusCode}: {errorBody}", statusCode);
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var records = ParseIndianApiResponse(json, cleanStock, _logger);

                _logger.LogInformation("Received {Count} historical ratio records for {Symbol} from IndianAPI", records.Count, cleanStock);
                return records;
            }
        }

        public static IReadOnlyList<IndianApiNormalizedRatioRecord> ParseIndianApiResponse(
            string json,
            string stockSymbol,
            ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ProviderApiException("IndianAPI returned an empty payload.");
            }

            var trimmed = json.Trim();
            if (trimmed == "{}" || trimmed == "[]")
            {
                throw new ProviderApiException("IndianAPI returned an empty payload object.");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ProviderApiException("IndianAPI response format is not a JSON object.");
            }

            var debtorDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var inventoryDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var payableDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var cccDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var wcDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var roceDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

            var allRawPeriods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Sometimes the API wraps the data in a "ratios" root node
            var dataRoot = root;
            if (root.TryGetProperty("ratios", out var ratiosObj) && ratiosObj.ValueKind == JsonValueKind.Object)
            {
                dataRoot = ratiosObj;
            }

            foreach (var categoryProp in dataRoot.EnumerateObject())
            {
                var categoryName = categoryProp.Name.Trim().ToLowerInvariant();
                if (categoryProp.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var periodProp in categoryProp.Value.EnumerateObject())
                {
                    var rawPeriod = periodProp.Name.Trim();
                    allRawPeriods.Add(rawPeriod);

                    if (categoryName.Contains("debtor"))
                    {
                        debtorDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("inventory"))
                    {
                        inventoryDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("payable"))
                    {
                        payableDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("conversion"))
                    {
                        cccDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("capital"))
                    {
                        wcDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("roce"))
                    {
                        roceDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                }
            }

            if (allRawPeriods.Count == 0)
            {
                throw new ProviderApiException("No valid historical ratio records could be parsed from IndianAPI response.");
            }

            var normalizedRecords = new List<IndianApiNormalizedRatioRecord>();

            foreach (var rawPeriod in allRawPeriods)
            {
                var (periodKey, periodDate, normalizedPeriod) = IndianApiNormalizedQuarterRecord.ResolvePeriod(rawPeriod);

                if (string.IsNullOrWhiteSpace(periodKey))
                {
                    logger?.LogWarning("Skipping unresolvable period '{RawPeriod}' for stock {Symbol}", rawPeriod, stockSymbol);
                    continue;
                }

                debtorDict.TryGetValue(rawPeriod, out var debtor);
                inventoryDict.TryGetValue(rawPeriod, out var inventory);
                payableDict.TryGetValue(rawPeriod, out var payable);
                cccDict.TryGetValue(rawPeriod, out var ccc);
                wcDict.TryGetValue(rawPeriod, out var wc);
                roceDict.TryGetValue(rawPeriod, out var roce);

                normalizedRecords.Add(new IndianApiNormalizedRatioRecord
                {
                    Period = normalizedPeriod,
                    PeriodKey = periodKey,
                    PeriodDate = periodDate,
                    PeriodType = "Annual",
                    DebtorDays = debtor,
                    InventoryDays = inventory,
                    PayableDays = payable,
                    CashConversionCycle = ccc,
                    WorkingCapitalDays = wc,
                    RocePercentage = roce,
                    Source = "IndianAPI"
                });
            }

            return normalizedRecords
                .OrderByDescending(r => r.PeriodDate)
                .ThenByDescending(r => r.PeriodKey)
                .ToList();
        }

        private static decimal? ParseDecimalValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var decVal))
            {
                return Math.Round(decVal, 2);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var str = element.GetString()?.Trim().TrimEnd('%');
                if (!string.IsNullOrWhiteSpace(str) && decimal.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedDec))
                {
                    return Math.Round(parsedDec, 2);
                }
            }

            return null;
        }
    }
}
