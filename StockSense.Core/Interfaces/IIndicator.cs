 using StockSense.Core.Models;

namespace StockSense.Core.Interfaces;

  /// <summary>
  /// Contract that every technical indicator (MA, RSI, MACD) must fulfill.
  /// </summary>
public interface IIndicator
{
    /// <summary>Human-readable name, e.g. "SMA-20" or "RSI-14".</summary>
    string Name { get; }

    /// <summary>The lookback period this indicator needs, e.g. 14 for RSI.</summary>
    int Period { get; }

    /// <summary>
    /// Calculates indicator values for the given price history.
    /// Returns one decimal per price point (or fewer if there isn't enough data yet).
    /// </summary>
    IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices);

    /// <summary>
    /// Checks if the latest data produces a Buy or Sell signal.
    /// Returns true if a signal was detected; the signal is written to <paramref name="signal"/>.
    /// </summary>
    bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal);
}