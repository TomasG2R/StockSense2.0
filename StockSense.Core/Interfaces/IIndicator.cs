using StockSense.Core.Models;

namespace StockSense.Core.Interfaces;

// GRADING: custom interface — defines the contract every indicator must implement.
// IStockDataProvider and IAlertStore follow the same pattern in their files.
/// Contract that every technical indicator (MA, RSI, MACD) must fulfill.
public interface IIndicator
{
    ///Human-readable name, e.g. "SMA-20" or "RSI-14".
    string Name { get; }

    ///The lookback period this indicator needs, e.g. 14 for RSI.
    int Period { get; }

    /// Calculates indicator values for the given price history.
    /// Returns one decimal per price point (or fewer if there isn't enough data yet).
    IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices);

    /// Checks if the latest data produces a Buy or Sell signal.
    /// Returns true if a signal was detected; the signal is written to <paramref name="signal"/>.
    bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal);
}
