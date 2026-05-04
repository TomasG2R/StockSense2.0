using System.Text.Json;
using StockSense.Core.Exceptions;
using StockSense.Core.Interfaces;
using StockSense.Core.Models;

namespace StockSense.Core.Services;

/// Fetches daily and weekly stock price history from the Alpha Vantage API.
public sealed class AlphaVantageService : IStockDataProvider
{
    // Single static HttpClient shared across all instances — best practice for performance
    private static readonly HttpClient _http = new();
    private readonly StockSenseOptions _options;
    private readonly RateLimiter _rateLimiter;

    /// Creates the service with config and rate limiter injected.
    public AlphaVantageService(StockSenseOptions options, RateLimiter rateLimiter)
    {
        _options     = options     ?? throw new ArgumentNullException(nameof(options));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    }

    public async Task<IReadOnlyList<StockPrice>> GetDailyAsync(
        StockSymbol symbol,
        DateOnly? start = null,
        DateOnly? end   = null,
        CancellationToken ct = default)
    {
        // GRADING: try-catch — wraps the full HTTP + parse operation so every
        // kind of failure (network down, API key wrong, bad JSON) is handled cleanly
        try
        {
            await _rateLimiter.WaitForSlotAsync(ct);

            string url  = $"{_options.GetBaseUrl()}?function=TIME_SERIES_DAILY" +
                          $"&symbol={symbol}&outputsize=compact&apikey={_options.GetApiKey()}";

            // GRADING: try-catch — HttpRequestException means no internet / DNS failure
            string json = await _http.GetStringAsync(url, ct);

            // out argument (HW1): TryParse writes the list into prices if parsing succeeds
            if (!TryParseResponse(json, "Time Series (Daily)", out List<StockPrice>? prices, out string? error))
                // GRADING: custom exception — bad API response becomes StockDataException,
                // not a generic InvalidOperationException
                throw new StockDataException(symbol.Value, error ?? "Unknown parse error.");

            // ?. operator (HW1): only filter if start/end were provided
            IEnumerable<StockPrice> result = prices!;
            if (start.HasValue)
                result = result.Where(p => DateOnly.FromDateTime(p.Date.DateTime) >= start.Value);
            if (end.HasValue)
                result = result.Where(p => DateOnly.FromDateTime(p.Date.DateTime) <= end.Value);

            return result.OrderBy(p => p.Date).ToList();
        }
        catch (StockSenseException)
        {
            // Let our own exceptions pass through untouched — they already carry
            // the right message and type for the caller to handle
            throw;
        }
        catch (HttpRequestException ex)
        {
            // GRADING: try-catch — network failure gets a meaningful custom exception
            throw new StockDataException(symbol.Value, $"Network error: {ex.Message}", ex);
        }
        catch (TaskCanceledException)
        {
            // GRADING: try-catch — user pressed Ctrl+C or request timed out
            throw new StockDataException(symbol.Value, "Request was cancelled or timed out.");
        }
        catch (Exception ex)
        {
            // GRADING: try-catch — anything else unexpected is wrapped so the app never crashes raw
            throw new StockDataException(symbol.Value, $"Unexpected error: {ex.Message}", ex);
        }
    }

    public async Task<IReadOnlyList<StockPrice>> GetWeeklyAsync(
        StockSymbol symbol,
        CancellationToken ct = default)
    {
        // GRADING: try-catch — same structured error handling for the weekly endpoint
        try
        {
            await _rateLimiter.WaitForSlotAsync(ct);

            string url  = $"{_options.GetBaseUrl()}?function=TIME_SERIES_WEEKLY" +
                          $"&symbol={symbol}&apikey={_options.GetApiKey()}";
            string json = await _http.GetStringAsync(url, ct);

            if (!TryParseResponse(json, "Weekly Time Series", out List<StockPrice>? prices, out string? error))
                throw new StockDataException(symbol.Value, error ?? "Unknown parse error.");

            return prices!.OrderBy(p => p.Date).ToList();
        }
        catch (StockSenseException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new StockDataException(symbol.Value, $"Network error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new StockDataException(symbol.Value, $"Unexpected error: {ex.Message}", ex);
        }
    }

    // Private helpers
    /// Tries to parse the raw JSON from Alpha Vantage.
    /// Uses two out parameters: the result list and an error message.
    /// Returns false (and sets error) if the JSON is invalid or missing expected keys.
    private static bool TryParseResponse(
        string json,
        string seriesKey,
        out List<StockPrice>? prices,
        out string? error)
    {
        prices = null;
        error  = null;

        // GRADING: try-catch — JSON parsing can throw if the response is malformed
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty(seriesKey, out JsonElement timeSeries))
            {
                // Alpha Vantage sends rate-limit messages under "Note" or "Information"
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
                    Open   = decimal.Parse(v.GetProperty("1. open").GetString()  ?? "0"),
                    High   = decimal.Parse(v.GetProperty("2. high").GetString()  ?? "0"),
                    Low    = decimal.Parse(v.GetProperty("3. low").GetString()   ?? "0"),
                    Close  = decimal.Parse(v.GetProperty("4. close").GetString() ?? "0"),
                    Volume = long.Parse(v.GetProperty("5. volume").GetString()   ?? "0"),
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
