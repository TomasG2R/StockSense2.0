namespace StockSense.Core.Services;

/// <summary>
/// Configuration options loaded from appsettings.json at startup.
/// </summary>
public sealed class StockSenseOptions
{
    /// <summary>Alpha Vantage API key. Required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Base URL for the Alpha Vantage API.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Maximum API requests allowed per minute.</summary>
    public int RequestsPerMinute { get; set; } = 5;

    /// <summary>Maximum API requests allowed per day.</summary>
    public int RequestsPerDay { get; set; } = 25;

    /// <summary>
    /// Returns the API key, or throws if it was never set.
    /// ??= operator: sets BaseUrl to the default only if it is currently null.
    /// </summary>
    public string GetApiKey()
    {
        // ??= operator: assign default only if null
        BaseUrl ??= "https://www.alphavantage.co/query";
        return ApiKey ?? throw new InvalidOperationException(
            "API key is not configured. Set ApiKey in appsettings.json.");
    }

    /// <summary>Returns the base URL, falling back to the default if null.</summary>
    public string GetBaseUrl()
    {
        // ?? operator: return right side if left side is null
        return BaseUrl ?? "https://www.alphavantage.co/query";
    }
}
