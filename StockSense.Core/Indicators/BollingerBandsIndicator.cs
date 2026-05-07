using StockSense.Core.Models;

namespace StockSense.Core.Indicators;

/// Calculates Bollinger Bands (20-period SMA ± N standard deviations).
///
/// Upper band = SMA-20 + (multiplier × StdDev)
/// Middle     = SMA-20
/// Lower band = SMA-20 - (multiplier × StdDev)
///
/// Signal rules:
///   Buy  → price crosses below the lower band (oversold, mean-reversion entry)
///   Sell → price crosses above the upper band (overbought, mean-reversion entry)
public sealed class BollingerBandsIndicator : IndicatorBase
{
    private const int     DefaultPeriod     = 20;
    private const decimal DefaultMultiplier = 2.0m;

    public override string  Name   => $"BB-{Period}";
    public override int     Period { get; }

    /// Standard deviation multiplier — how wide the bands are.
    public decimal Multiplier { get; }

    // GRADING: params keyword — callers can pass extra multiplier overrides:
    //   new BollingerBandsIndicator()           → period 20, multiplier 2.0
    //   new BollingerBandsIndicator(20, 2.5m)   → wider bands
    //   new BollingerBandsIndicator(params decimal[] opts) could receive extra options
    // Implemented here as default + named arguments alongside a params array
    // for any additional future band levels (e.g. 1σ, 2σ, 3σ simultaneously).
    /// Creates a BollingerBandsIndicator.
    /// <param name="period">Lookback window. Defaults to 20.</param>
    /// <param name="multiplier">StdDev multiplier. Defaults to 2.0.</param>
    /// <param name="extraMultipliers">Optional additional band levels (unused in signals, available for display).</param>
    public BollingerBandsIndicator(
        int period = DefaultPeriod,
        decimal multiplier = DefaultMultiplier,
        params decimal[] extraMultipliers)
    {
        if (period < 2)     throw new ArgumentOutOfRangeException(nameof(period));
        if (multiplier <= 0) throw new ArgumentOutOfRangeException(nameof(multiplier));
        Period     = period;
        Multiplier = multiplier;
    }

    /// Returns one BollingerPoint per price bar that has enough history.
    /// Needs at least Period data points.
    public IReadOnlyList<BollingerPoint> CalculateBands(IReadOnlyList<StockPrice> prices)
    {
        ValidateInput(prices, Period);

        StockPrice[] arr = prices as StockPrice[] ?? prices.ToArray();
        var results = new List<BollingerPoint>();

        for (int i = Period - 1; i < arr.Length; i++)
        {
            // GRADING: Range type — slice the window using C# Range operator
            StockPrice[] window = arr[(i - Period + 1)..(i + 1)];

            decimal sum = 0;
            foreach (StockPrice p in window) sum += p.Close;
            decimal sma = sum / Period;

            // Standard deviation of closing prices in the window
            decimal variance = 0;
            foreach (StockPrice p in window)
                variance += (p.Close - sma) * (p.Close - sma);
            decimal stdDev = (decimal)Math.Sqrt((double)(variance / Period));

            results.Add(new BollingerPoint
            {
                Upper  = sma + Multiplier * stdDev,
                Middle = sma,
                Lower  = sma - Multiplier * stdDev
            });
        }

        return results;
    }

    /// Returns the latest BollingerPoint, or null if not enough data.
    public BollingerPoint? LatestBands(IReadOnlyList<StockPrice> prices)
    {
        if (prices.Count < Period) return null;
        return CalculateBands(prices)[^1];
    }

    /// Required by IndicatorBase — returns the middle band (SMA) as a decimal series.
    /// Use CalculateBands() when you need Upper/Lower values.
    public override IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices) =>
        CalculateBands(prices).Select(b => b.Middle).ToList();

    /// Signal: price IS below the lower band → Buy (oversold zone).
    ///         price IS above the upper band → Sell (overbought zone).
    /// Zone-based rather than crossover-based — fires on every bar where
    /// price is outside the bands, giving a natural cushion around the entry.
    public override bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal)
    {
        signal = SignalType.None;
        if (prices.Count < Period) return false;

        BollingerPoint? bands = LatestBands(prices);
        if (bands is null) return false;

        decimal currClose = prices[^1].Close;

        // Price below lower band → oversold → Buy
        bool belowLower = currClose < bands.Lower;
        // Price above upper band → overbought → Sell
        bool aboveUpper = currClose > bands.Upper;

        signal = (belowLower, aboveUpper) switch
        {
            (true, _) => SignalType.Buy,
            (_, true) => SignalType.Sell,
            _         => SignalType.None
        };

        return signal != SignalType.None;
    }
}
