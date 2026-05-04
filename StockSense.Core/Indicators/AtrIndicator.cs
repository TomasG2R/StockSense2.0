using StockSense.Core.Models;

namespace StockSense.Core.Indicators;

/// Calculates Average True Range (ATR) — measures daily price volatility.
/// ATR tells you how much a stock typically moves per day.
/// Used to set stop-loss and take-profit levels that fit the stock's actual behaviour.
public sealed class AtrIndicator : IndicatorBase
{
    private const int DefaultPeriod = 14;

    public override string Name => $"ATR-{Period}";
    public override int Period { get; }

    public AtrIndicator(int period = DefaultPeriod)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// Returns one ATR value per price point that has enough history.
    /// Uses Wilder's smoothing (same method as RSI) for stable results.
    public override IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices)
    {
        ValidateInput(prices, Period + 1);

        // Step 1: compute True Range for every day after the first
        // TR = largest of: (High-Low), |High-PrevClose|, |Low-PrevClose|
        var trueRanges = new List<decimal>();
        for (int i = 1; i < prices.Count; i++)
        {
            decimal highLow       = prices[i].High - prices[i].Low;
            decimal highPrevClose = Math.Abs(prices[i].High - prices[i - 1].Close);
            decimal lowPrevClose  = Math.Abs(prices[i].Low  - prices[i - 1].Close);
            trueRanges.Add(Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose)));
        }

        // Step 2: seed ATR with a simple average of the first Period true ranges
        decimal seed = 0;
        for (int i = 0; i < Period; i++) seed += trueRanges[i];
        seed /= Period;

        var results = new List<decimal> { seed };

        // Step 3: Wilder's smoothing — each ATR value smooths in the new TR
        // ATR[i] = (ATR[i-1] * (Period-1) + TR[i]) / Period
        for (int i = Period; i < trueRanges.Count; i++)
        {
            decimal atr = (results[^1] * (Period - 1) + trueRanges[i]) / Period;
            results.Add(atr);
        }

        return results;
    }

    /// ATR does not produce Buy/Sell signals — it measures volatility only.
    public override bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal)
    {
        signal = SignalType.None;
        return false;
    }

    /// Returns the latest ATR value, or null if there is not enough data.
    public decimal? LatestAtr(IReadOnlyList<StockPrice> prices)
    {
        if (prices.Count < Period + 1) return null;
        return Calculate(prices)[^1];
    }
}
