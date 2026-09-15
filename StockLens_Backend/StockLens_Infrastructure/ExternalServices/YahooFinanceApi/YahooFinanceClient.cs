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

        public async Task<System.Collections.Generic.List<StockLens_Infrastructure.ExternalServices.IndianApi.Models.IndianApiPriceRecord>> GetHistoricalPricesAsync(string symbol, string exchange, CancellationToken cancellationToken = default)
        {
            var records = new System.Collections.Generic.List<StockLens_Infrastructure.ExternalServices.IndianApi.Models.IndianApiPriceRecord>();
            try
            {
                var suffix = exchange.Equals("BSE", StringComparison.OrdinalIgnoreCase) ? ".BO" : ".NS";
                var cleanSymbol = symbol.Trim().ToUpperInvariant();
                var searchSymbol = cleanSymbol.EndsWith(".NS") || cleanSymbol.EndsWith(".BO") ? cleanSymbol : $"{cleanSymbol}{suffix}";

                var endpoint = $"/v8/finance/chart/{Uri.EscapeDataString(searchSymbol)}?range=5y&interval=1d";
                _logger.LogInformation("Fetching historical price chart from Yahoo Finance API for {Symbol}", searchSymbol);

                var response = await _httpClient.GetAsync(endpoint, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Yahoo Finance Chart API returned status code {StatusCode} for {Symbol}", response.StatusCode, searchSymbol);
                    return records;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("chart", out var chartObj) &&
                    chartObj.TryGetProperty("result", out var resultArr) &&
                    resultArr.ValueKind == JsonValueKind.Array &&
                    resultArr.GetArrayLength() > 0)
                {
                    var firstRes = resultArr[0];
                    if (firstRes.TryGetProperty("timestamp", out var tsArr) &&
                        firstRes.TryGetProperty("indicators", out var indObj) &&
                        indObj.TryGetProperty("quote", out var quoteArr) &&
                        quoteArr.ValueKind == JsonValueKind.Array &&
                        quoteArr.GetArrayLength() > 0)
                    {
                        var quote = quoteArr[0];
                        quote.TryGetProperty("open", out var openArr);
                        quote.TryGetProperty("high", out var highArr);
                        quote.TryGetProperty("low", out var lowArr);
                        quote.TryGetProperty("close", out var closeArr);
                        quote.TryGetProperty("volume", out var volArr);

                        var count = tsArr.GetArrayLength();
                        for (int i = 0; i < count; i++)
                        {
                            var ts = tsArr[i].GetInt64();
                            var date = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;

                            decimal? open = GetDecimalAt(openArr, i);
                            decimal? high = GetDecimalAt(highArr, i);
                            decimal? low = GetDecimalAt(lowArr, i);
                            decimal? close = GetDecimalAt(closeArr, i);
                            long? vol = GetLongAt(volArr, i);

                            if (close.HasValue && close.Value > 0)
                            {
                                records.Add(new StockLens_Infrastructure.ExternalServices.IndianApi.Models.IndianApiPriceRecord
                                {
                                    DateString = date.ToString("yyyy-MM-dd"),
                                    Open = open ?? close.Value,
                                    High = high ?? close.Value,
                                    Low = low ?? close.Value,
                                    Close = close.Value,
                                    Volume = vol ?? 0
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching historical prices from Yahoo Finance API for {Symbol}", symbol);
            }

            return records;
        }

        public async Task<YahooLiveQuoteDto?> GetLiveQuoteAsync(string symbol, string? exchange = "NSE", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return null;

            try
            {
                var suffix = (exchange != null && exchange.Equals("BSE", StringComparison.OrdinalIgnoreCase)) ? ".BO" : ".NS";
                var cleanSymbol = symbol.Trim().ToUpperInvariant();
                var searchSymbol = cleanSymbol.EndsWith(".NS") || cleanSymbol.EndsWith(".BO") ? cleanSymbol : $"{cleanSymbol}{suffix}";

                var endpoint = $"/v8/finance/chart/{Uri.EscapeDataString(searchSymbol)}?range=1d&interval=1m";
                _logger.LogInformation("Fetching live quote for {Symbol} via Yahoo Finance API", searchSymbol);

                var response = await _httpClient.GetAsync(endpoint, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Yahoo Finance API live quote returned status {StatusCode} for {Symbol}", response.StatusCode, searchSymbol);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("chart", out var chartObj) &&
                    chartObj.TryGetProperty("result", out var resultArr) &&
                    resultArr.ValueKind == JsonValueKind.Array &&
                    resultArr.GetArrayLength() > 0)
                {
                    var firstRes = resultArr[0];
                    if (firstRes.TryGetProperty("meta", out var meta))
                    {
                        var quote = new YahooLiveQuoteDto { Symbol = cleanSymbol };

                        if (meta.TryGetProperty("regularMarketPrice", out var p) && p.TryGetDecimal(out var price)) quote.Price = price;
                        else if (meta.TryGetProperty("fulldayPrice", out var fp) && fp.TryGetDecimal(out var fprice)) quote.Price = fprice;

                        if (meta.TryGetProperty("regularMarketDayHigh", out var dh) && dh.TryGetDecimal(out var dayHigh)) quote.DayHigh = dayHigh;
                        if (meta.TryGetProperty("regularMarketDayLow", out var dl) && dl.TryGetDecimal(out var dayLow)) quote.DayLow = dayLow;
                        if (meta.TryGetProperty("fiftyTwoWeekHigh", out var yh) && yh.TryGetDecimal(out var yearHigh)) quote.YearHigh = yearHigh;
                        if (meta.TryGetProperty("fiftyTwoWeekLow", out var yl) && yl.TryGetDecimal(out var yearLow)) quote.YearLow = yearLow;
                        if (meta.TryGetProperty("previousClose", out var pc) && pc.TryGetDecimal(out var prevClose)) quote.PreviousClose = prevClose;
                        if (meta.TryGetProperty("regularMarketChangePercent", out var cp) && cp.TryGetDecimal(out var changePercent)) quote.ChangePercent = changePercent;
                        if (meta.TryGetProperty("regularMarketVolume", out var v) && v.TryGetInt64(out var vol)) quote.Volume = vol;

                        if (quote.Price.HasValue && quote.Price.Value > 0)
                        {
                            return quote;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching live quote from Yahoo Finance API for {Symbol}", symbol);
            }

            return null;
        }

        private static decimal? GetDecimalAt(JsonElement array, int index)
        {
            if (array.ValueKind == JsonValueKind.Array && index < array.GetArrayLength())
            {
                var el = array[index];
                if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d))
                {
                    return d;
                }
            }
            return null;
        }

        private static long? GetLongAt(JsonElement array, int index)
        {
            if (array.ValueKind == JsonValueKind.Array && index < array.GetArrayLength())
            {
                var el = array[index];
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var l))
                {
                    return l;
                }
            }
            return null;
        }
    }
}
