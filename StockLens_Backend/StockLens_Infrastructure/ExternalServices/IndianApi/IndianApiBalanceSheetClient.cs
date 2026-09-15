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
    public class IndianApiBalanceSheetClient : IIndianApiBalanceSheetClient
    {
        private readonly HttpClient _httpClient;
        private readonly IndianApiSettings _settings;
        private readonly ILogger<IIndianApiBalanceSheetClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        public IndianApiBalanceSheetClient(
            HttpClient httpClient,
            IOptions<IndianApiSettings> settings,
            ILogger<IIndianApiBalanceSheetClient> logger)
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
                Console.WriteLine($"IndianAPI response status code: {response.StatusCode} for symbol: {cleanStock}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("IndianAPI returned status {StatusCode} for symbol {Symbol}", response.StatusCode, symbol);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"IndianAPI response content for {cleanStock}: {json}");
                var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal?>>>(json, JsonOptions);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching balance sheet for {Symbol} from IndianAPI.", symbol);
                return null;
            }
        }

        public async Task<decimal?> GetCurrentPriceAsync(string symbol, string? exchange = "NSE", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return null;
            }

            var cleanStock = symbol.Trim().ToUpperInvariant();
            var endpoint = $"stock?name={Uri.EscapeDataString(cleanStock)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                var cleanApiKey = System.Text.RegularExpressions.Regex.Replace(_settings.ApiKey, @"[^\x20-\x7E]", "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanApiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", cleanApiKey);
                }
            }

            _logger.LogInformation("Fetching current price from IndianAPI GET /stock for symbol: {Symbol}", cleanStock);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("IndianAPI /stock returned status {StatusCode} for symbol {Symbol}", response.StatusCode, cleanStock);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                JsonElement target = root;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    target = root[0];
                }

                if (target.ValueKind == JsonValueKind.Object)
                {
                    // 1. Check currentPrice object (e.g., {"NSE": 2980.50, "BSE": 2975.00})
                    if (target.TryGetProperty("currentPrice", out var cpProp) || target.TryGetProperty("current_price", out cpProp))
                    {
                        if (cpProp.ValueKind == JsonValueKind.Object)
                        {
                            var cleanExchange = string.IsNullOrWhiteSpace(exchange) ? "NSE" : exchange.Trim().ToUpperInvariant();
                            if (cpProp.TryGetProperty(cleanExchange, out var exVal) && TryExtractDecimal(exVal, out var price))
                            {
                                return price;
                            }
                            if (cpProp.TryGetProperty("NSE", out var nseVal) && TryExtractDecimal(nseVal, out price))
                            {
                                return price;
                            }
                            if (cpProp.TryGetProperty("BSE", out var bseVal) && TryExtractDecimal(bseVal, out price))
                            {
                                return price;
                            }
                            foreach (var prop in cpProp.EnumerateObject())
                            {
                                if (TryExtractDecimal(prop.Value, out price)) return price;
                            }
                        }
                        else if (TryExtractDecimal(cpProp, out var price))
                        {
                            return price;
                        }
                    }

                    // 2. Check direct price property
                    if (target.TryGetProperty("price", out var pProp) && TryExtractDecimal(pProp, out var p))
                    {
                        return p;
                    }

                    // 3. Check stockTechnicalData.lastTradedPrice
                    if (target.TryGetProperty("stockTechnicalData", out var techProp) && techProp.ValueKind == JsonValueKind.Object)
                    {
                        if (techProp.TryGetProperty("lastTradedPrice", out var ltp) && TryExtractDecimal(ltp, out var techPrice))
                        {
                            return techPrice;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching current price for {Symbol} from IndianAPI.", cleanStock);
                return null;
            }
        }

        public async Task<IndianApiStockOverviewDto?> GetStockFinancialsAndOverviewAsync(
            string symbol,
            string? exchange = "NSE",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return null;
            }

            var cleanStock = symbol.Trim().ToUpperInvariant();
            var endpoint = $"stock?name={Uri.EscapeDataString(cleanStock)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                var cleanApiKey = System.Text.RegularExpressions.Regex.Replace(_settings.ApiKey, @"[^\x20-\x7E]", "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanApiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", cleanApiKey);
                }
            }

            _logger.LogInformation("Fetching stock overview & financials from IndianAPI GET /stock for symbol: {Symbol}", cleanStock);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("IndianAPI /stock returned status {StatusCode} for symbol {Symbol}", response.StatusCode, cleanStock);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                JsonElement target = root;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    target = root[0];
                }

                if (target.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var dto = new IndianApiStockOverviewDto
                {
                    Symbol = cleanStock
                };

                if (target.TryGetProperty("companyName", out var cnProp) && cnProp.ValueKind == JsonValueKind.String)
                {
                    dto.CompanyName = cnProp.GetString();
                }

                if (target.TryGetProperty("industry", out var indProp) && indProp.ValueKind == JsonValueKind.String)
                {
                    dto.Industry = indProp.GetString();
                    dto.SectorName = dto.Industry;
                }
                else if (target.TryGetProperty("companyProfile", out var cpProf) && cpProf.ValueKind == JsonValueKind.Object)
                {
                    if (cpProf.TryGetProperty("mgIndustry", out var mgInd) && mgInd.ValueKind == JsonValueKind.String)
                    {
                        dto.Industry = mgInd.GetString();
                        dto.SectorName = dto.Industry;
                    }
                }

                // Current price
                if (target.TryGetProperty("currentPrice", out var cpProp) || target.TryGetProperty("current_price", out cpProp))
                {
                    if (cpProp.ValueKind == JsonValueKind.Object)
                    {
                        var cleanExchange = string.IsNullOrWhiteSpace(exchange) ? "NSE" : exchange.Trim().ToUpperInvariant();
                        if (cpProp.TryGetProperty(cleanExchange, out var exVal) && TryExtractDecimal(exVal, out var price))
                        {
                            dto.CurrentPrice = price;
                        }
                        else if (cpProp.TryGetProperty("NSE", out var nseVal) && TryExtractDecimal(nseVal, out price))
                        {
                            dto.CurrentPrice = price;
                        }
                        else if (cpProp.TryGetProperty("BSE", out var bseVal) && TryExtractDecimal(bseVal, out price))
                        {
                            dto.CurrentPrice = price;
                        }
                    }
                    else if (TryExtractDecimal(cpProp, out var price))
                    {
                        dto.CurrentPrice = price;
                    }
                }

                if (!dto.CurrentPrice.HasValue && target.TryGetProperty("price", out var pProp) && TryExtractDecimal(pProp, out var p))
                {
                    dto.CurrentPrice = p;
                }

                if (target.TryGetProperty("yearHigh", out var yhProp) && TryExtractDecimal(yhProp, out var yh))
                {
                    dto.YearHigh = yh;
                }

                if (target.TryGetProperty("yearLow", out var ylProp) && TryExtractDecimal(ylProp, out var yl))
                {
                    dto.YearLow = yl;
                }

                // Peers & Sector PE
                if (target.TryGetProperty("peerCompanyList", out var peerList) && peerList.ValueKind == JsonValueKind.Array)
                {
                    var peList = new List<decimal>();
                    foreach (var peerEl in peerList.EnumerateArray())
                    {
                        var peer = new IndianApiPeerDto();
                        if (peerEl.TryGetProperty("companyName", out var pName)) peer.CompanyName = pName.GetString();
                        if (peerEl.TryGetProperty("price", out var pPrice) && TryExtractDecimal(pPrice, out var pp)) peer.Price = pp;
                        if (peerEl.TryGetProperty("priceToEarningsValueRatio", out var pPe) && TryExtractDecimal(pPe, out var pe))
                        {
                            peer.PeRatio = pe;
                            if (pe > 0 && pe < 300) peList.Add(pe);
                        }
                        if (peerEl.TryGetProperty("priceToBookValueRatio", out var pPb) && TryExtractDecimal(pPb, out var pb)) peer.PbRatio = pb;
                        if (peerEl.TryGetProperty("marketCap", out var pMc) && TryExtractDecimal(pMc, out var mc)) peer.MarketCap = mc;
                        if (peerEl.TryGetProperty("returnOnAverageEquityTrailing12Month", out var pRoe) && TryExtractDecimal(pRoe, out var roe)) peer.Roe = roe;
                        if (peerEl.TryGetProperty("dividendYieldIndicatedAnnualDividend", out var pDiv) && TryExtractDecimal(pDiv, out var div)) peer.DividendYield = div;
                        if (peerEl.TryGetProperty("totalSharesOutstanding", out var pShares) && TryExtractDecimal(pShares, out var shares)) peer.TotalShares = shares;

                        dto.Peers.Add(peer);
                    }

                    if (peList.Count > 0)
                    {
                        dto.SectorPe = Math.Round(System.Linq.Enumerable.Average(peList), 2);
                    }
                }

                // Parse Financials
                if (target.TryGetProperty("financials", out var finArray) && finArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var finEl in finArray.EnumerateArray())
                    {
                        var periodType = "Annual";
                        if (finEl.TryGetProperty("Type", out var tProp) && tProp.ValueKind == JsonValueKind.String)
                        {
                            periodType = tProp.GetString() ?? "Annual";
                        }

                        if (!periodType.Equals("Annual", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var period = new IndianApiFinancialPeriodDto
                        {
                            PeriodType = "annual"
                        };

                        if (finEl.TryGetProperty("FiscalYear", out var fyProp) && fyProp.ValueKind == JsonValueKind.String)
                        {
                            var rawFy = fyProp.GetString() ?? "";
                            if (rawFy.Length == 4 && rawFy.StartsWith("20"))
                            {
                                period.FiscalYear = $"FY{rawFy.Substring(2)}";
                            }
                            else
                            {
                                period.FiscalYear = rawFy.StartsWith("FY", StringComparison.OrdinalIgnoreCase) ? rawFy : $"FY{rawFy}";
                            }
                        }

                        if (finEl.TryGetProperty("EndDate", out var edProp) && edProp.ValueKind == JsonValueKind.String)
                        {
                            if (DateTime.TryParse(edProp.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var ed))
                            {
                                period.PeriodEndDate = ed;
                            }
                        }

                        if (finEl.TryGetProperty("stockFinancialMap", out var map) && map.ValueKind == JsonValueKind.Object)
                        {
                            // 1. Cash Flow Statement (CAS)
                            if (map.TryGetProperty("CAS", out var casArray) && casArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in casArray.EnumerateArray())
                                {
                                    if (item.TryGetProperty("key", out var k) && item.TryGetProperty("value", out var v))
                                    {
                                        var key = k.GetString();
                                        if (key == "CashfromOperatingActivities" && TryExtractDecimal(v, out var cfo))
                                            period.OperatingCashFlow = cfo;
                                        else if (key == "CapitalExpenditures" && TryExtractDecimal(v, out var capex))
                                            period.Capex = Math.Abs(capex); // Normalize to positive amount
                                        else if (key == "NetChangeinCash" && TryExtractDecimal(v, out var netCash))
                                            period.NetCashFlow = netCash;
                                    }
                                }
                            }

                            // 2. Income Statement (INC)
                            if (map.TryGetProperty("INC", out var incArray) && incArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in incArray.EnumerateArray())
                                {
                                    if (item.TryGetProperty("key", out var k) && item.TryGetProperty("value", out var v))
                                    {
                                        var key = k.GetString();
                                          if ((key == "TotalRevenue" || key == "Revenue") && TryExtractDecimal(v, out var rev))
                                              period.Revenue = rev;
                                          else if ((key == "OperatingIncome" || key == "OperatingProfit") && TryExtractDecimal(v, out var op))
                                              period.OperatingProfit = op;
                                          else if ((key == "NetIncomeBeforeTaxes" || key == "IncomeBeforeTaxes" || key == "EarningsBeforeTaxes") && TryExtractDecimal(v, out var pbt))
                                              period.ProfitBeforeTax = pbt;
                                          else if (key == "NetIncome" && TryExtractDecimal(v, out var np))
                                              period.NetProfit = np;
                                         else if (key == "NetIncomeAfterTaxes" && TryExtractDecimal(v, out var npat) && !period.NetProfit.HasValue)
                                             period.NetProfit = npat;
                                         else if ((key == "DilutedEPSExcludingExtraOrdItems" || key == "DilutedNormalizedEPS" || key == "DilutedEPS") && TryExtractDecimal(v, out var eps))
                                             period.Eps = eps;
                                         else if (key == "MinorityInterest" && TryExtractDecimal(v, out var mi))
                                             period.NetProfitAttributableToMinorityInterest = Math.Abs(mi);
                                    }
                                }
                            }

                            // 3. Balance Sheet (BAL)
                            if (map.TryGetProperty("BAL", out var balArray) && balArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in balArray.EnumerateArray())
                                {
                                    if (item.TryGetProperty("key", out var k) && item.TryGetProperty("value", out var v))
                                    {
                                        var key = k.GetString();
                                        if (key == "TotalAssets" && TryExtractDecimal(v, out var ta))
                                            period.TotalAssets = ta;
                                        else if (key == "TotalLiabilities" && TryExtractDecimal(v, out var tl))
                                            period.TotalLiabilities = tl;
                                        else if (key == "TotalCurrentLiabilities" && TryExtractDecimal(v, out var tcl))
                                            period.TotalCurrentLiabilities = tcl;
                                        else if ((key == "TotalDebt" || key == "TotalLongTermDebt") && TryExtractDecimal(v, out var td))
                                            period.TotalDebt = td;
                                        else if (key == "TotalEquity" && TryExtractDecimal(v, out var te))
                                            period.TotalEquity = te;
                                        else if (key == "OtherEquityTotal" && TryExtractDecimal(v, out var oe))
                                            period.OtherEquity = oe;
                                        else if (key == "CommonStockTotal" && TryExtractDecimal(v, out var cs))
                                            period.EquityCapital = cs;
                                        else if (key == "TangibleBookValueperShareCommonEq" && TryExtractDecimal(v, out var tbv))
                                            period.BookValuePerShare = tbv;
                                        else if (key == "TotalCommonSharesOutstanding" && TryExtractDecimal(v, out var tcso))
                                            period.TotalShares = tcso;
                                    }
                                }

                                // Prefer Consolidated Book Value Per Share = TotalEquity / TotalShares if available
                                if (period.TotalEquity.HasValue && period.TotalShares.HasValue && period.TotalShares.Value > 0)
                                {
                                    period.BookValuePerShare = Math.Round(period.TotalEquity.Value / period.TotalShares.Value, 2);
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(period.FiscalYear))
                        {
                            dto.Financials.Add(period);
                        }
                    }
                }

                decimal? directBvQuarter = null;
                decimal? directBvFiscalYear = null;

                // Parse keyMetrics for TTM EPS, Consolidated Book Value & Valuation multiples
                if (target.TryGetProperty("keyMetrics", out var kmProp) && kmProp.ValueKind == JsonValueKind.Object)
                {
                    if (kmProp.TryGetProperty("persharedata", out var psdArray) && psdArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in psdArray.EnumerateArray())
                        {
                            if (item.TryGetProperty("key", out var k) && item.TryGetProperty("value", out var v))
                            {
                                var key = k.GetString();
                                if ((key == "eEPSExcludingExtraordinaryIitemsTrailing12onth" ||
                                     key == "ePSIncludingExtraOrdinaryItemsTrailing12Month" ||
                                     key == "ePSBasicExcludingExtraordinaryItemsItrailing12Month") &&
                                    TryExtractDecimal(v, out var ttmEps) && ttmEps > 0)
                                {
                                    dto.TtmEps = ttmEps;
                                }
                                else if (key == "bookValuePerShareMostRecentQuarter" && TryExtractDecimal(v, out var bvq) && bvq > 0)
                                {
                                    directBvQuarter = bvq;
                                }
                                else if (key == "bookValuePerShare MostRecentFiscalYear" && TryExtractDecimal(v, out var bvf) && bvf > 0)
                                {
                                    directBvFiscalYear = bvf;
                                }
                            }
                        }
                    }

                    if (!dto.TtmEps.HasValue && kmProp.TryGetProperty("incomeStatement", out var isArray) && isArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in isArray.EnumerateArray())
                        {
                            if (item.TryGetProperty("key", out var k) && item.TryGetProperty("value", out var v))
                            {
                                var key = k.GetString();
                                if (key == "earningsPerShareNormalizedExcludingExtraordinaryItemsAvgDilutedSharesOutstandingTTM" &&
                                    TryExtractDecimal(v, out var ttmEps) && ttmEps > 0)
                                {
                                    dto.TtmEps = ttmEps;
                                    break;
                                }
                            }
                        }
                    }
                }

                // Sort financials descending by period end date or fiscal year
                dto.Financials = System.Linq.Enumerable.ToList(
                    System.Linq.Enumerable.OrderByDescending(dto.Financials, f => f.PeriodEndDate ?? DateTime.MinValue));

                // Derive top level valuation ratios from latest period / TTM metrics
                var latest = System.Linq.Enumerable.FirstOrDefault(dto.Financials);
                var effectiveEps = dto.TtmEps ?? latest?.Eps;

                dto.BookValue = directBvQuarter ?? directBvFiscalYear ?? latest?.BookValuePerShare;
                if (latest != null && dto.BookValue.HasValue)
                {
                    latest.BookValuePerShare = dto.BookValue.Value;
                }

                if (dto.CurrentPrice.HasValue && effectiveEps.HasValue && effectiveEps.Value > 0)
                {
                    // Formula: P/E = Current Share Market Price / TTM EPS
                    dto.PeRatio = Math.Round(dto.CurrentPrice.Value / effectiveEps.Value, 2);
                }

                var effectiveBookValue = dto.BookValue ?? latest?.BookValuePerShare;
                if (dto.CurrentPrice.HasValue && effectiveBookValue.HasValue && effectiveBookValue.Value > 0)
                {
                    // Formula: P/B = Current Share Market Price / Book Value
                    dto.PbRatio = Math.Round(dto.CurrentPrice.Value / effectiveBookValue.Value, 2);
                }

                if (latest != null)
                {
                    if (latest.NetProfit.HasValue && latest.TotalEquity.HasValue && latest.TotalEquity.Value > 0)
                    {
                        dto.Roe = Math.Round((latest.NetProfit.Value / latest.TotalEquity.Value) * 100, 2);
                    }

                    var ebit = latest.ProfitBeforeTax ?? latest.OperatingProfit;
                    if (ebit.HasValue && ebit.Value > 0)
                    {
                        var priorPeriod = dto.Financials.Count > 1 ? dto.Financials[1] : null;

                        decimal? currentCe = null;
                        if (latest.TotalEquity.HasValue && (latest.TotalEquity.Value + (latest.TotalDebt ?? 0)) > 0)
                        {
                            currentCe = latest.TotalEquity.Value + (latest.TotalDebt ?? 0);
                        }
                        else if (latest.TotalAssets.HasValue && latest.TotalCurrentLiabilities.HasValue && (latest.TotalAssets.Value - latest.TotalCurrentLiabilities.Value) > 0)
                        {
                            currentCe = latest.TotalAssets.Value - latest.TotalCurrentLiabilities.Value;
                        }

                        decimal? priorCe = null;
                        if (priorPeriod != null)
                        {
                            if (priorPeriod.TotalEquity.HasValue && (priorPeriod.TotalEquity.Value + (priorPeriod.TotalDebt ?? 0)) > 0)
                            {
                                priorCe = priorPeriod.TotalEquity.Value + (priorPeriod.TotalDebt ?? 0);
                            }
                            else if (priorPeriod.TotalAssets.HasValue && priorPeriod.TotalCurrentLiabilities.HasValue && (priorPeriod.TotalAssets.Value - priorPeriod.TotalCurrentLiabilities.Value) > 0)
                            {
                                priorCe = priorPeriod.TotalAssets.Value - priorPeriod.TotalCurrentLiabilities.Value;
                            }
                        }

                        // Use Opening Capital Employed (or Closing CE if single period) for standard ROCE
                        var capitalEmployed = priorCe ?? currentCe;

                        if (capitalEmployed.HasValue && capitalEmployed.Value > 0)
                        {
                            // Formula: ROCE = (EBIT / Capital Employed) * 100
                            dto.Roce = Math.Round((ebit.Value / capitalEmployed.Value) * 100, 2);
                        }
                    }

                    if (dto.CurrentPrice.HasValue && latest.TotalShares.HasValue && latest.TotalShares.Value > 0)
                    {
                        dto.MarketCap = Math.Round(dto.CurrentPrice.Value * latest.TotalShares.Value, 2);
                    }
                    if (latest.EquityCapital.HasValue && latest.TotalShares.HasValue && latest.TotalShares.Value > 0)
                    {
                        dto.FaceValue = Math.Round(latest.EquityCapital.Value / latest.TotalShares.Value, 2);
                    }
                }

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching stock financials and overview from IndianAPI for {Symbol}.", cleanStock);
                return null;
            }
        }

        private static bool TryExtractDecimal(JsonElement el, out decimal val)
        {
            val = 0;
            if (el.ValueKind == JsonValueKind.Number)
            {
                return el.TryGetDecimal(out val);
            }
            if (el.ValueKind == JsonValueKind.String)
            {
                var str = el.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    str = str.Replace(",", "").Replace("₹", "").Trim();
                    return decimal.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val);
                }
            }
            return false;
        }
    }
}
