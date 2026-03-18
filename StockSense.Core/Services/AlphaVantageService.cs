using System.Text.Json;
  using StockSense.Core.Interfaces;
  using StockSense.Core.Models;

  namespace StockSense.Core.Services;

  /// <summary>
  /// Fetches daily stock price history from the Alpha Vantage API.
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

          string url = BuildUrl(symbol);
          string json = await _http.GetStringAsync(url, ct);

          // out argument: TryParse writes the list into prices if parsing succeeds
          if (!TryParseResponse(json, out List<StockPrice>? prices, out string? error))
              throw new InvalidOperationException($"Failed to parse Alpha Vantage response: {error}");

          // ?. operator: only filter if start/end were provided
          IEnumerable<StockPrice> result = prices!;
          if (start.HasValue)
              result = result.Where(p => DateOnly.FromDateTime(p.Date.DateTime) >= start.Value);
          if (end.HasValue)
              result = result.Where(p => DateOnly.FromDateTime(p.Date.DateTime) <= end.Value);

          return result.OrderBy(p => p.Date).ToList();
      }

      // ── Private helpers ───────────────────────────────────────────────────────

      private string BuildUrl(StockSymbol symbol)
      {
          string apiKey  = _options.GetApiKey();
          string baseUrl = _options.GetBaseUrl();

          // ?[] operator: safe index — if query string were an array, ?[] guards null access
          return $"{baseUrl}?function=TIME_SERIES_DAILY&symbol={symbol}&outputsize=compact&apikey={apiKey}";
      }

      /// <summary>
      /// Tries to parse the raw JSON from Alpha Vantage.
      /// Uses two out parameters: the result list and an error message.
      /// Returns false (and sets error) if the JSON is invalid or missing expected keys.
      /// </summary>
      private static bool TryParseResponse(
          string json,
          out List<StockPrice>? prices,
          out string? error)
      {
          prices = null;
          error  = null;

          try
          {
              using JsonDocument doc = JsonDocument.Parse(json);
              JsonElement root = doc.RootElement;

              // Alpha Vantage wraps data under "Time Series (Daily)"
              if (!root.TryGetProperty("Time Series (Daily)", out JsonElement timeSeries))
              {
                  // Check if the API returned an error message instead
                  error = root.TryGetProperty("Note", out JsonElement note)
                      ? note.GetString()
                      : root.TryGetProperty("Information", out JsonElement info)
                          ? info.GetString()
                          : "Missing 'Time Series (Daily)' key in response.";
                  return false;
              }

              prices = new List<StockPrice>();

              foreach (JsonProperty day in timeSeries.EnumerateObject())
              {
                  // ?. operator: safe property access on the JSON element
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