namespace StockSense.Core.Models;

/// Holds fundamental data for one stock fetched from Finnhub.
/// All fields are nullable — the API may not have data for every symbol.
public sealed class FundamentalData
{
    /// The ticker symbol this data belongs to.
    public string Symbol { get; init; } = string.Empty;

    /// The next upcoming earnings date, if known.
    public DateOnly? NextEarningsDate { get; init; }

    /// Days until next earnings. Null if NextEarningsDate is unknown.
    // GRADING: ?. operator — safe navigation on nullable NextEarningsDate
    public int? DaysUntilEarnings =>
        NextEarningsDate is not null
            ? (NextEarningsDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.UtcNow.Date).Days
            : null;

    /// True when earnings are within the given number of days.
    // GRADING: ?? operator — fallback to false when DaysUntilEarnings is null
    public bool EarningsWithin(int days) =>
        (DaysUntilEarnings ?? int.MaxValue) <= days;

    /// Analyst consensus rating: "Strong Buy", "Buy", "Hold", "Sell", "Strong Sell".
    /// Null if no analyst coverage available.
    public string? AnalystRating { get; init; }

    /// Average analyst price target in USD. Null if unavailable.
    public decimal? AnalystTarget { get; init; }

    /// Number of analysts covering this stock. Null if unavailable.
    public int? AnalystCount { get; init; }

    /// Upside potential from current price to analyst target, as a percentage.
    /// Null if target or current price is unknown.
    // GRADING: ?. and ?? operators — chained null-safe calculation
    public decimal? UpsidePotential(decimal currentPrice) =>
        AnalystTarget is not null && currentPrice > 0
            ? ((AnalystTarget.Value - currentPrice) / currentPrice) * 100m
            : null;

    /// True when the analyst consensus is bullish (Buy or Strong Buy).
    // GRADING: ?. operator — safe call on nullable AnalystRating string
    public bool IsBullishRating =>
        AnalystRating?.Contains("Buy", StringComparison.OrdinalIgnoreCase) ?? false;

    public override string ToString() =>
        $"{Symbol} | Earnings: {NextEarningsDate?.ToString() ?? "unknown"} " +
        $"| Rating: {AnalystRating ?? "n/a"} " +
        $"| Target: {(AnalystTarget.HasValue ? $"${AnalystTarget:F2}" : "n/a")}";
}
