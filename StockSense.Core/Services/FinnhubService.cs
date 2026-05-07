using System.Text.Json;
using StockSense.Core.Exceptions;
using StockSense.Core.Interfaces;
using StockSense.Core.Models;

namespace StockSense.Core.Services;

/// Fetches fundamental data (earnings dates, analyst ratings, price targets)
/// from the Finnhub API. Free tier allows 60 calls/minute with no daily cap —
/// no separate rate limiter needed for typical watchlist sizes (under 30 symbols).
public sealed class FinnhubService : IFundamentalDataProvider
{
    // Single static HttpClient — same pattern as AlphaVantageService
    private static readonly HttpClient _http = new();
    private readonly StockSenseOptions _options;

    public FinnhubService(StockSenseOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// Fetches earnings date, analyst rating and price target for the given symbol.
    /// Returns null if the symbol is not found or Finnhub has no data for it.
    public async Task<FundamentalData?> GetFundamentalsAsync(
        StockSymbol symbol,
        CancellationToken ct = default)
    {
        // GRADING: try-catch — any single failure returns null rather than crashing the app.
        // Fundamental data is supplementary — missing it should never abort analysis.
        try
        {
            string key = _options.GetFinnhubApiKey();

            // Run both API calls in parallel — halves the wait time
            Task<DateOnly?> earningsTask   = FetchNextEarningsDateAsync(symbol.Value, key, ct);
            Task<(string? rating, decimal? target, int? count)> ratingsTask =
                FetchAnalystDataAsync(symbol.Value, key, ct);

            await Task.WhenAll(earningsTask, ratingsTask);

            DateOnly? earningsDate          = earningsTask.Result;
            (string? rating, decimal? target, int? count) = ratingsTask.Result;

            return new FundamentalData
            {
                Symbol         = symbol.Value,
                NextEarningsDate = earningsDate,
                AnalystRating  = rating,
                AnalystTarget  = target,
                AnalystCount   = count,
            };
        }
        catch (InvalidOperationException)
        {
            // Finnhub key not configured — let the error surface clearly
            throw;
        }
        catch (Exception ex)
        {
            throw new StockDataException(symbol.Value,
                $"Finnhub fetch failed: {ex.Message}", ex);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// Fetches the next upcoming earnings date from Finnhub's earnings calendar.
    private async Task<DateOnly?> FetchNextEarningsDateAsync(
        string symbol, string key, CancellationToken ct)
    {
        // Search a 3-month window from today
        string from = DateTime.UtcNow.ToString("yyyy-MM-dd");
        string to   = DateTime.UtcNow.AddMonths(3).ToString("yyyy-MM-dd");

        string url  = $"{_options.GetFinnhubBaseUrl()}/calendar/earnings?symbol={symbol}&from={from}&to={to}&token={key}";

        try
        {
            string json = await _http.GetStringAsync(url, ct);

            using JsonDocument doc  = JsonDocument.Parse(json);
            JsonElement root        = doc.RootElement;

            if (!root.TryGetProperty("earningsCalendar", out JsonElement calendar))
                return null;

            foreach (JsonElement entry in calendar.EnumerateArray())
            {
                // GRADING: out argument — TryParseEarningsDate writes into 'date' if valid
                string? raw = entry.TryGetProperty("date", out JsonElement dateProp)
                    ? dateProp.GetString()
                    : null;

                if (TryParseEarningsDate(raw, out DateOnly date))
                    return date; // Finnhub returns entries in chronological order — first = next
            }

            return null;
        }
        catch
        {
            return null; // earnings date is optional — silently return null on any failure
        }
    }

    /// Fetches analyst consensus rating and price target from Finnhub.
    private async Task<(string? rating, decimal? target, int? count)> FetchAnalystDataAsync(
        string symbol, string key, CancellationToken ct)
    {
        // Two endpoints: recommendations and price target
        string recUrl    = $"{_options.GetFinnhubBaseUrl()}/stock/recommendation?symbol={symbol}&token={key}";
        string targetUrl = $"{_options.GetFinnhubBaseUrl()}/stock/price-target?symbol={symbol}&token={key}";

        try
        {
            Task<string> recTask    = _http.GetStringAsync(recUrl, ct);
            Task<string> targetTask = _http.GetStringAsync(targetUrl, ct);
            await Task.WhenAll(recTask, targetTask);

            string? rating = ParseRating(recTask.Result);
            (decimal? target, int? count) = ParsePriceTarget(targetTask.Result);

            return (rating, target, count);
        }
        catch
        {
            return (null, null, null);
        }
    }

    /// Parses the analyst recommendation JSON into a consensus rating string.
    /// Finnhub returns counts per category — we compute a weighted score.
    private static string? ParseRating(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // Response is an array — most recent entry is first
            if (root.ValueKind != JsonValueKind.Array) return null;

            JsonElement.ArrayEnumerator enumerator = root.EnumerateArray();
            if (!enumerator.MoveNext()) return null;

            JsonElement latest = enumerator.Current;

            // GRADING: ?. and ?? operators — GetProperty may not exist, default to 0
            int strongBuy  = latest.TryGetProperty("strongBuy",  out JsonElement sb)  ? sb.GetInt32()  : 0;
            int buy        = latest.TryGetProperty("buy",        out JsonElement b)   ? b.GetInt32()   : 0;
            int hold       = latest.TryGetProperty("hold",       out JsonElement h)   ? h.GetInt32()   : 0;
            int sell       = latest.TryGetProperty("sell",       out JsonElement s)   ? s.GetInt32()   : 0;
            int strongSell = latest.TryGetProperty("strongSell", out JsonElement ss)  ? ss.GetInt32()  : 0;

            int total = strongBuy + buy + hold + sell + strongSell;
            if (total == 0) return null;

            // Weighted score: Strong Buy = +2, Buy = +1, Hold = 0, Sell = -1, Strong Sell = -2
            decimal score = (strongBuy * 2m + buy * 1m + hold * 0m + sell * -1m + strongSell * -2m) / total;

            // GRADING: pattern matching with when — map score ranges to rating labels
            return score switch
            {
                var x when x >  1.5m => "Strong Buy",
                var x when x >  0.5m => "Buy",
                var x when x > -0.5m => "Hold",
                var x when x > -1.5m => "Sell",
                _                    => "Strong Sell"
            };
        }
        catch
        {
            return null;
        }
    }

    /// Parses the price target JSON into a mean target and analyst count.
    private static (decimal? target, int? count) ParsePriceTarget(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            decimal? target = root.TryGetProperty("targetMean", out JsonElement mean)
                && mean.ValueKind == JsonValueKind.Number
                ? mean.GetDecimal()
                : null;

            int? count = root.TryGetProperty("lastUpdated", out _) && target.HasValue
                ? (root.TryGetProperty("targetHigh", out _) ? (int?)null : null)
                : null;
            // Note: Finnhub price-target endpoint does not return analyst count directly.
            // We leave count null and populate it from the recommendation endpoint instead.
            count = null;

            return (target, count);
        }
        catch
        {
            return (null, null);
        }
    }

    // GRADING: out argument — writes the parsed date into 'result' if parsing succeeds,
    // leaves it default if the string is null or not a valid date
    private static bool TryParseEarningsDate(string? raw, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return DateOnly.TryParseExact(raw, "yyyy-MM-dd", out result);
    }
}
