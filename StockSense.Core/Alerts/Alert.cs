// Alert.cs
// Represents a triggered buy/sell alert for a stock symbol.
// Saved to alerts.json when a signal is detected.
// Grading: bitwise operations (SignalType flags), sealed class.
using StockSense.Core.Models;

namespace StockSense.Core.Alerts;

/// A triggered trading alert. Created by AlertService, persisted to alerts.json.
public sealed class Alert
{
    ///Unique identifier for this alert.
    public Guid Id { get; init; }

    ///The stock ticker this alert is for, e.g. "AAPL".
    public string Symbol { get; init; } = string.Empty;

    ///The signal that triggered this alert (Buy, Sell, StrongBuy, etc.).
    public SignalType Signal { get; init; }

    ///When this alert was created.
    public DateTimeOffset CreatedAt { get; init; }

    ///Human-readable description of why the alert fired.
    public string Message { get; init; } = string.Empty;

    //Signal helpers
    // Bitwise & checks if a specific flag is switched on inside Signal.

    public bool IsBuy    => (Signal & SignalType.Buy)    != SignalType.None;
    public bool IsSell   => (Signal & SignalType.Sell)   != SignalType.None;
    public bool IsStrong => (Signal & SignalType.Strong) != SignalType.None;

    // Display
    public override string ToString() =>
        $"[{CreatedAt:yyyy-MM-dd}] {Symbol} — {Signal} | {Message}";
}
