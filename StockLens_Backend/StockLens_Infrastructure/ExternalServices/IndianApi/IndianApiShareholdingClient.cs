using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockLens_Infrastructure.ExternalServices.BharatStock.Exceptions;
using StockLens_Infrastructure.ExternalServices.IndianApi.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StockLens_Infrastructure.ExternalServices.IndianApi
{
    public class IndianApiShareholdingClient : IIndianApiShareholdingClient
    {
        private readonly HttpClient _httpClient;
        private readonly IndianApiSettings _settings;
        private readonly ILogger<IndianApiShareholdingClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        public IndianApiShareholdingClient(
            HttpClient httpClient,
            IOptions<IndianApiSettings> settings,
            ILogger<IndianApiShareholdingClient> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<IndianApiNormalizedQuarterRecord>> GetQuarterlyShareholdingAsync(
            string stockName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(stockName))
            {
                throw new ArgumentException("Stock name/symbol is required.", nameof(stockName));
            }

            var cleanStock = stockName.Trim().ToUpperInvariant();
            var endpoint = $"historical_stats?stock_name={Uri.EscapeDataString(cleanStock)}&stats=shareholding_pattern_quarterly";

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                var cleanApiKey = Regex.Replace(_settings.ApiKey, @"[^\x20-\x7E]", "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanApiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", cleanApiKey);
                }
            }

            _logger.LogInformation("Fetching quarterly shareholding for {Symbol} from IndianAPI via {Endpoint}", cleanStock, endpoint);

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
                    throw new ProviderNotFoundException(cleanStock, $"Historical shareholding data not found for stock '{cleanStock}' in IndianAPI.");
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

                _logger.LogInformation("Received {Count} historical shareholding records for {Symbol} from IndianAPI", records.Count, cleanStock);
                return records;
            }
        }

        public static IReadOnlyList<IndianApiNormalizedQuarterRecord> ParseIndianApiResponse(
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

            // Dictionaries for each known category: PeriodKey -> value
            var promotersDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var fiisDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var diisDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var govtDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var publicDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var othersDict = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var shareholdersDict = new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);

            // Set of all distinct raw periods discovered across all categories
            var allRawPeriods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var categoryProp in root.EnumerateObject())
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

                    if (categoryName.Contains("promoter"))
                    {
                        promotersDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("fii") || categoryName.Contains("fpi"))
                    {
                        fiisDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("dii"))
                    {
                        diisDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("government") || categoryName.Contains("govt"))
                    {
                        govtDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("public"))
                    {
                        publicDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("other"))
                    {
                        othersDict[rawPeriod] = ParseDecimalValue(periodProp.Value);
                    }
                    else if (categoryName.Contains("shareholder"))
                    {
                        shareholdersDict[rawPeriod] = ParseLongValue(periodProp.Value);
                    }
                }
            }

            if (allRawPeriods.Count == 0)
            {
                throw new ProviderApiException("No valid historical quarter records could be parsed from IndianAPI response.");
            }

            var normalizedRecords = new List<IndianApiNormalizedQuarterRecord>();

            foreach (var rawPeriod in allRawPeriods)
            {
                var (periodKey, periodDate, normalizedPeriod) = IndianApiNormalizedQuarterRecord.ResolvePeriod(rawPeriod);

                if (string.IsNullOrWhiteSpace(periodKey))
                {
                    logger?.LogWarning("Skipping unresolvable period '{RawPeriod}' for stock {Symbol}", rawPeriod, stockSymbol);
                    continue;
                }

                promotersDict.TryGetValue(rawPeriod, out var promoter);
                fiisDict.TryGetValue(rawPeriod, out var fii);
                diisDict.TryGetValue(rawPeriod, out var dii);
                govtDict.TryGetValue(rawPeriod, out var govt);
                publicDict.TryGetValue(rawPeriod, out var pub);
                othersDict.TryGetValue(rawPeriod, out var other);
                shareholdersDict.TryGetValue(rawPeriod, out var shareholders);

                if (!fii.HasValue && logger != null)
                {
                    logger.LogWarning("FII/FPI data unavailable for {Symbol} {PeriodKey}", stockSymbol, periodKey);
                }
                if (!dii.HasValue && logger != null)
                {
                    logger.LogWarning("DII data unavailable for {Symbol} {PeriodKey}", stockSymbol, periodKey);
                }

                normalizedRecords.Add(new IndianApiNormalizedQuarterRecord
                {
                    Period = normalizedPeriod,
                    PeriodKey = periodKey,
                    PeriodDate = periodDate,
                    PeriodType = "Quarterly",
                    PromoterHolding = promoter,
                    FiiHolding = fii,
                    DiiHolding = dii,
                    GovernmentHolding = govt,
                    PublicHolding = pub,
                    OtherHolding = other,
                    ShareholdersCount = shareholders,
                    Source = "IndianAPI"
                });
            }

            // Sort descending by PeriodDate / PeriodKey (latest first)
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
                return Math.Round(Math.Max(0.00m, Math.Min(100.00m, decVal)), 2);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var str = element.GetString()?.Trim().TrimEnd('%');
                if (!string.IsNullOrWhiteSpace(str) && decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDec))
                {
                    return Math.Round(Math.Max(0.00m, Math.Min(100.00m, parsedDec)), 2);
                }
            }

            return null;
        }

        private static long? ParseLongValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt64(out var lVal)) return lVal;
                if (element.TryGetDouble(out var dVal)) return (long)Math.Round(dVal);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var str = element.GetString()?.Trim().Replace(",", "");
                if (!string.IsNullOrWhiteSpace(str) && double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDouble))
                {
                    return (long)Math.Round(parsedDouble);
                }
            }

            return null;
        }
    }
}
