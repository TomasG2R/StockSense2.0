using StockSense.Core.Interfaces;
using StockSense.Core.Models;

namespace StockSense.Core.Alerts;

/// <summary>
/// Triggers and stores alerts when a signal is detected for a stock.
/// </summary>
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

    /// <summary>Message templates keyed by signal type. Set in static constructor.</summary>
    public static Dictionary<SignalType, string> MessageTemplates { get; }

    // Delegate: any subscriber can listen for new alerts
    public delegate void AlertTriggeredHandler(Alert alert);

    /// <summary>Fires whenever a new alert is created and saved.</summary>
    public event AlertTriggeredHandler? OnAlertTriggered;

    private readonly IAlertStore _store;

    /// <summary>Creates the service with an alert store injected.</summary>
    public AlertService(IAlertStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Creates and saves an alert for the given symbol and signal.
    /// Fires OnAlertTriggered after saving.
    /// </summary>
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

    /// <summary>Returns all saved alerts, optionally filtered by symbol.</summary>
    public Task<IReadOnlyList<Alert>> GetHistoryAsync(
        StockSymbol? symbol = null,
        CancellationToken ct = default) =>
        _store.LoadAsync(symbol, ct);
}
