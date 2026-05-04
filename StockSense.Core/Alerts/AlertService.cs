using StockSense.Core.Indicators;
using StockSense.Core.Interfaces;
using StockSense.Core.Models;

namespace StockSense.Core.Alerts;

/// Triggers and stores alerts when a signal is detected for a stock.
public sealed class AlertService
{
    // Static constructor — runs once before AlertService is first used anywhere.
    // Sets up the default message templates for each signal type.
    static AlertService()
    {
        MessageTemplates = new Dictionary<SignalType, string>
        {
            [SignalType.Buy]                       = "Buy signal detected",
            [SignalType.Sell]                      = "Sell signal detected",
            [SignalType.Buy  | SignalType.Strong]  = "Strong buy — multiple indicators agree",
            [SignalType.Sell | SignalType.Strong]  = "Strong sell — multiple indicators agree",
            [SignalType.Buy  | SignalType.Weak]    = "Weak buy signal",
            [SignalType.Sell | SignalType.Weak]    = "Weak sell signal",
        };
    }

    ///Message templates keyed by signal type. Set in static constructor.
    public static Dictionary<SignalType, string> MessageTemplates { get; }

    // Delegate: any subscriber can listen for new alerts
    public delegate void AlertTriggeredHandler(Alert alert);

    ///Fires whenever a new alert is created and saved.
    public event AlertTriggeredHandler? OnAlertTriggered;

    private readonly IAlertStore _store;
    private readonly AtrIndicator _atr = new();

    /// Creates the service with an alert store injected.
    public AlertService(IAlertStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// Creates and saves an alert for the given symbol and signal.
    /// Calculates ATR-based entry, stop-loss, and take-profit when prices are provided.
    /// Fires OnAlertTriggered after saving.
    public async Task TriggerAsync(
        string symbol,
        SignalType signal,
        IReadOnlyList<StockPrice> prices,
        CancellationToken ct = default)
    {
        if (signal == SignalType.None) return;

        string message = MessageTemplates.TryGetValue(signal, out string? template)
            ? template
            : signal.ToString();

        decimal? entry    = prices.Count > 0 ? prices[^1].Close : null;
        decimal? atr      = _atr.LatestAtr(prices);
        decimal? stopLoss = null;
        decimal? target   = null;

        if (entry is not null && atr is not null)
        {
            bool isBuy = (signal & SignalType.Buy) != SignalType.None;
            stopLoss = isBuy
                ? entry - 1.5m * atr      // stop below entry for Buy
                : entry + 1.5m * atr;     // stop above entry for Sell
            target = isBuy
                ? entry + 3.0m * atr      // target above entry for Buy
                : entry - 3.0m * atr;     // target below entry for Sell
        }

        var alert = new Alert
        {
            Id        = Guid.NewGuid(),
            Symbol    = symbol,
            Signal    = signal,
            CreatedAt = DateTimeOffset.UtcNow,
            Message   = message,
            Entry     = entry,
            StopLoss  = stopLoss,
            Target    = target,
        };

        await _store.SaveAsync(alert, ct);

        // Invoke the delegate — notifies all subscribers (e.g. Console can print it live)
        OnAlertTriggered?.Invoke(alert);
    }

    ///Returns all saved alerts, optionally filtered by symbol.
    public Task<IReadOnlyList<Alert>> GetHistoryAsync(
        StockSymbol? symbol = null,
        CancellationToken ct = default) =>
        _store.LoadAsync(symbol, ct);
}
