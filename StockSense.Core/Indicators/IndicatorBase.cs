using StockSense.Core.Interfaces;
using StockSense.Core.Models;

namespace StockSense.Core.Indicators;

/// Shared base for all technical indicators. Cannot be instantiated directly.
public abstract class IndicatorBase : IIndicator
{

    public abstract string Name { get; }
    public abstract int Period { get; }

    public abstract IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices);

    public abstract bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal);

    /// Throws if the price list is null or has fewer entries than the required period.
    /// Call this at the top of every Calculate/TryGetSignal implementation.
    protected void ValidateInput(IReadOnlyList<StockPrice> prices, int requiredLength)
    {
        ArgumentNullException.ThrowIfNull(prices);
        if (prices.Count < requiredLength)
            throw new ArgumentException(
                $"{Name} needs at least {requiredLength} data points, got {prices.Count}.");
    }
}
