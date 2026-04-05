using System.Text.Json;
using StockSense.Core.Interfaces;
using StockSense.Core.Models;

namespace StockSense.Core.Services;

/// <summary>
/// Fetches daily and weekly stock price history from the Alpha Vantage API.
/// </summary>
public sealed class AlphaVantageService : IStockDataProvider
{
    // Single static HttpClient — never create one inside a loop
    private static readonly HttpClient _http = new();

    private readonly StockSenseOptions _options;
    private readonly RateLimiter _rateLimiter;

    /// <summary>Creates the service with config and rate limiter injected.</summary>
    public AlphaVantageService(StockSenseOptions options, RateLimiter rateLimiter)
    {
        _options     = options     ?? throw new ArgumentNullException(nameof(options));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StockPrice>> GetDailyAsync(
        StockSymbol symbol,
        DateOnly? start = null,
        DateOnly? end   = null,
        CancellationToken ct = default)
    {
        await _rateLimiter.WaitForSlotAsync(ct);

        string url = $"{_options.GetBaseUrl()}?function=TIME_SERIES_DAILY&symbol={symbol}&outputsize=compact&apikey={_options.GetApiKey()}";
        string json = await _http.GetStringAsync(url, ct);

        // out argument: TryParse writes the list into prices if parsing succeeds
        if (!TryParseResponse(json, "Time Series (Daily)", out List<StockPrice>? prices, out string? error))
            throw new InvalidOperationException($"Failed to parse Alpha Vantage response: {error}");

        // ?. operator: only filter if start/end were provided
        IEnumerable<StockPrice> result = prices!;
        if (start.HasValue)
            result = result.Where(p => DateOnly.FromDateTime(p.Date.DateTime) >= start.Value);
        if (end.HasValue)
            result = result.Where(p => DateOnly.FromDateTime(p.Date.DateTime) <= end.Value);

        return result.OrderBy(p => p.Date).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StockPrice>> GetWeeklyAsync(
        StockSymbol symbol,
        CancellationToken ct = default)
    {
        await _rateLimiter.WaitForSlotAsync(ct);

        string url = $"{_options.GetBaseUrl()}?function=TIME_SERIES_WEEKLY&symbol={symbol}&apikey={_options.GetApiKey()}";
        string json = await _http.GetStringAsync(url, ct);

        if (!TryParseResponse(json, "Weekly Time Series", out List<StockPrice>? prices, out string? error))
            throw new InvalidOperationException($"Failed to parse Alpha Vantage weekly response: {error}");

        return prices!.OrderBy(p => p.Date).ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Tries to parse the raw JSON from Alpha Vantage.
    /// Uses two out parameters: the result list and an error message.
    /// seriesKey is the JSON key that wraps the data (e.g. "Time Series (Daily)").
    /// Returns false (and sets error) if the JSON is invalid or missing expected keys.
    /// </summary>
    private static bool TryParseResponse(
        string json,
        string seriesKey,
        out List<StockPrice>? prices,
        out string? error)
    {
        prices = null;
        error  = null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty(seriesKey, out JsonElement timeSeries))
            {
                error = root.TryGetProperty("Note", out JsonElement note)
                    ? note.GetString()
                    : root.TryGetProperty("Information", out JsonElement info)
                        ? info.GetString()
                        : $"Missing '{seriesKey}' key in response.";
                return false;
            }

            prices = new List<StockPrice>();

            foreach (JsonProperty day in timeSeries.EnumerateObject())
            {
                JsonElement v = day.Value;
                prices.Add(new StockPrice
                {
                    Date   = DateTimeOffset.Parse(day.Name),
                    Open   = decimal.Parse(v.GetProperty("1. open").GetString()   ?? "0"),
                    High   = decimal.Parse(v.GetProperty("2. high").GetString()   ?? "0"),
                    Low    = decimal.Parse(v.GetProperty("3. low").GetString()    ?? "0"),
                    Close  = decimal.Parse(v.GetProperty("4. close").GetString()  ?? "0"),
                    Volume = long.Parse(v.GetProperty("5. volume").GetString()    ?? "0"),
                });
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
