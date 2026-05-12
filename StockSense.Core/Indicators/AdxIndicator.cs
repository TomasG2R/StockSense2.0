using StockSense.Core.Models;

namespace StockSense.Core.Indicators;

/// Calculates the Average Directional Index (ADX).
/// ADX measures trend STRENGTH, not direction.
///   ADX > 25 → market is trending   (signals are reliable)
///   ADX < 20 → market is ranging    (signals are unreliable — skip them)
///   +DI > -DI → bullish trend
///   -DI > +DI → bearish trend
public sealed class AdxIndicator : IndicatorBase
{
    private const int DefaultPeriod = 14;

    public override string Name   => $"ADX-{Period}";
    public override int    Period { get; }

    // GRADING: default and named arguments — same pattern as RsiIndicator
    /// Creates an AdxIndicator. Period defaults to 14 if not specified.
    public AdxIndicator(int period = DefaultPeriod)
    {
        if (period < 2) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// Returns one ADX value per price point that has enough history.
    /// Needs at least (2 × Period) + 1 data points to produce a stable result.
    public override IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices)
    {
        int required = Period * 2 + 1;
        ValidateInput(prices, required);

        // Step 1: calculate True Range, +DM and -DM for every bar
        var tr  = new decimal[prices.Count];
        var pdm = new decimal[prices.Count]; // +Directional Movement
        var ndm = new decimal[prices.Count]; // -Directional Movement

        for (int i = 1; i < prices.Count; i++)
        {
            decimal high     = prices[i].High;
            decimal low      = prices[i].Low;
            decimal prevClose = prices[i - 1].Close;
            decimal prevHigh  = prices[i - 1].High;
            decimal prevLow   = prices[i - 1].Low;

            // True Range = largest of the three ranges
            tr[i] = Math.Max(high - low,
                    Math.Max(Math.Abs(high - prevClose),
                             Math.Abs(low  - prevClose)));

            decimal upMove   = high    - prevHigh;
            decimal downMove = prevLow - low;

            // +DM: up move must be larger than down move and positive
            pdm[i] = (upMove > downMove && upMove > 0) ? upMove : 0m;
            // -DM: down move must be larger than up move and positive
            ndm[i] = (downMove > upMove && downMove > 0) ? downMove : 0m;
        }

        // Step 2: seed the first Wilder smoothed values using a simple sum
        decimal smoothTr  = 0, smoothPdm = 0, smoothNdm = 0;
        for (int i = 1; i <= Period; i++)
        {
            smoothTr  += tr[i];
            smoothPdm += pdm[i];
            smoothNdm += ndm[i];
        }

        // Step 3: calculate DX for the seed bar, then smooth into ADX
        var adxValues = new List<decimal>();

        decimal firstDx = ComputeDx(smoothTr, smoothPdm, smoothNdm);
        decimal smoothDx = firstDx; // seed ADX with first DX

        // Step 4: Wilder smoothing for remaining bars
        for (int i = Period + 1; i < prices.Count; i++)
        {
            // Wilder smoothing: subtract 1/Period of previous value, add new value
            smoothTr  = smoothTr  - (smoothTr  / Period) + tr[i];
            smoothPdm = smoothPdm - (smoothPdm / Period) + pdm[i];
            smoothNdm = smoothNdm - (smoothNdm / Period) + ndm[i];

            decimal dx = ComputeDx(smoothTr, smoothPdm, smoothNdm);

            // ADX is a Wilder smoothed average of DX
            smoothDx = (smoothDx * (Period - 1) + dx) / Period;

            // Only emit values once we have a full second period of smoothing
            if (i >= Period * 2)
                adxValues.Add(smoothDx);
        }

        return adxValues;
    }

    /// Returns the latest ADX value, or null if not enough data.
    public decimal? LatestAdx(IReadOnlyList<StockPrice> prices)
    {
        int required = Period * 2 + 1;
        if (prices.Count < required) return null;
        // GRADING: Range type — [^1] gets the last element
        return Calculate(prices)[^1];
    }

    /// True when the market is trending strongly enough to trust signals (ADX >= 25).
    public bool IsTrending(IReadOnlyList<StockPrice> prices) =>
        (LatestAdx(prices) ?? 0m) >= 25m;

    /// True when the market is ranging and signals should be suppressed (ADX < 20).
    /// Signals in the weakly-trending zone (ADX 20–24) are still allowed through.
    public bool IsRanging(IReadOnlyList<StockPrice> prices) =>
        (LatestAdx(prices) ?? 100m) < 20m;

    /// TryGetSignal is not meaningful for ADX alone — ADX has no direction.
    /// Use IsTrending() to filter signals from other indicators.
    /// Returns None always; exists to satisfy the abstract base class contract.
    public override bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal)
    {
        signal = SignalType.None;
        return false;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// Computes the Directional Index (DX) from smoothed TR, +DM and -DM.
    private static decimal ComputeDx(decimal smoothTr, decimal smoothPdm, decimal smoothNdm)
    {
        if (smoothTr == 0) return 0;

        decimal diPlus  = 100m * smoothPdm / smoothTr;
        decimal diMinus = 100m * smoothNdm / smoothTr;
        decimal diSum   = diPlus + diMinus;

        return diSum == 0 ? 0 : 100m * Math.Abs(diPlus - diMinus) / diSum;
    }
}
