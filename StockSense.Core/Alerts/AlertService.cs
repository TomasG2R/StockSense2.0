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

    /// Creates the service with an alert store injected.
    public AlertService(IAlertStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// Creates and saves an alert for the given symbol and signal.
    /// Fires OnAlertTriggered after saving.
    public async Task TriggerAsync(
        string symbol,
        SignalType signal,
        CancellationToken ct = default)
    {
        if (signal == SignalType.None) return;

        string message = MessageTemplates.TryGetValue(signal, out string? template)
            ? template
            : signal.ToString();

        var alert = new Alert
        {
            Id        = Guid.NewGuid(),
            Symbol    = symbol,
            Signal    = signal,
            CreatedAt = DateTimeOffset.UtcNow,
            Message   = message,
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
