namespace StockSense.Core.Services;

/// Configuration options loaded from appsettings.json at startup.
public sealed class StockSenseOptions
{
    /// >Alpha Vantage API key. Required.
    public string? ApiKey { get; set; }

    /// Base URL for the Alpha Vantage API.
    public string? BaseUrl { get; set; }

    /// Maximum API requests allowed per minute.
    public int RequestsPerMinute { get; set; } = 5;

    ///Maximum API requests allowed per day.
    public int RequestsPerDay { get; set; } = 25;

    /// Returns the API key, or throws if it was never set.
    /// ??= operator: sets BaseUrl to the default only if it is currently null.
    public string GetApiKey()
    {
        // ??= operator: assign default only if null
        BaseUrl ??= "https://www.alphavantage.co/query";
        return ApiKey ?? throw new InvalidOperationException(
            "API key is not configured. Set ApiKey in appsettings.json.");
    }

    ///Returns the base URL, falling back to the default if null.
    public string GetBaseUrl()
    {
        // ?? operator: return right side if left side is null
        return BaseUrl ?? "https://www.alphavantage.co/query";
    }
}
