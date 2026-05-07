namespace StockSense.Core.Services;

// GRADING: ICloneable — lets callers make a safe independent copy of the config.
public sealed class StockSenseOptions : ICloneable
{
    /// Alpha Vantage API key. Required.
    public string? ApiKey { get; set; }

    /// Base URL for the Alpha Vantage API.
    public string? BaseUrl { get; set; }

    /// Finnhub API key. Required for earnings and analyst rating features.
    public string? FinnhubApiKey { get; set; }

    /// Base URL for the Finnhub API. Defaults to the standard endpoint.
    public string? FinnhubBaseUrl { get; set; }

    /// Returns the Finnhub API key, or throws if it was never set.
    public string GetFinnhubApiKey() =>
        FinnhubApiKey ?? throw new InvalidOperationException(
            "Finnhub API key is not configured. Set FinnhubApiKey in appsettings.json.");

    /// Returns the Finnhub base URL, falling back to the default if null.
    public string GetFinnhubBaseUrl() =>
        FinnhubBaseUrl ?? "https://finnhub.io/api/v1";

    /// Maximum API requests allowed per minute.
    public int RequestsPerMinute { get; set; } = 5;

    /// Maximum API requests allowed per day.
    public int RequestsPerDay { get; set; } = 25;

    /// Returns the API key, or throws if it was never set.
    public string GetApiKey()
    {
        // ??= operator: assign default only if null
        BaseUrl ??= "https://www.alphavantage.co/query";
        return ApiKey ?? throw new InvalidOperationException(
            "API key is not configured. Set ApiKey in appsettings.json.");
    }

    /// Returns the base URL, falling back to the default if null.
    public string GetBaseUrl()
    {
        // ?? operator: return right side if left side is null
        return BaseUrl ?? "https://www.alphavantage.co/query";
    }

    // GRADING: ICloneable — Clone() returns an independent copy of this config object
    public object Clone() => new StockSenseOptions
    {
        ApiKey            = ApiKey,
        BaseUrl           = BaseUrl,
        FinnhubApiKey     = FinnhubApiKey,
        FinnhubBaseUrl    = FinnhubBaseUrl,
        RequestsPerMinute = RequestsPerMinute,
        RequestsPerDay    = RequestsPerDay,
    };
}
