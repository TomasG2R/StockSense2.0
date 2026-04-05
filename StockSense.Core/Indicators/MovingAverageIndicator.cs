using StockSense.Core.Models;

namespace StockSense.Core.Indicators;

/// <summary>
/// Calculates Simple Moving Average (SMA) and Exponential Moving Average (EMA).
/// </summary>
public sealed class MovingAverageIndicator : IndicatorBase
{
    private const int DefaultPeriod = 20;
    private static readonly MovingAverageIndicator _sma20 = new(20);
    private static readonly MovingAverageIndicator _sma50 = new(50);
    public override string Name => $"SMA-{Period}";
    
    public override int Period { get; }

    /// <summary>Creates a MovingAverageIndicator with a custom period.</summary>
    public MovingAverageIndicator(int period = DefaultPeriod)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>
    /// Returns the SMA value for each position that has enough history.
    /// Uses Range slicing on the underlying array to get each price window.
    /// </summary>
    public override IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices)
    {
        ValidateInput(prices, Period);

        // Convert to array so we can use Range slicing: array[start..end]
        StockPrice[] arr = prices as StockPrice[] ?? prices.ToArray();
        var results = new List<decimal>();

        for (int i = Period - 1; i < arr.Length; i++)
        {
            // Range type requirement: slice the window of prices for this period
            StockPrice[] window = arr[(i - Period + 1)..(i + 1)];
            decimal sum = 0;
            foreach (StockPrice p in window) sum += p.Close;
            results.Add(sum / Period);
        }

        return results;
    }

    /// <summary>
    /// Calculates EMA for the given period.
    /// First EMA value is seeded with a simple average (not zero).
    /// </summary>
    public IReadOnlyList<decimal> CalculateEma(IReadOnlyList<StockPrice> prices)
    {
        ValidateInput(prices, Period);

        StockPrice[] arr = prices as StockPrice[] ?? prices.ToArray();
        var results = new List<decimal>();
        decimal multiplier = 2m / (Period + 1);

        // Range type: seed slice arr[..Period] = first Period elements
        StockPrice[] seedWindow = arr[..Period];
        decimal seed = 0;
        foreach (StockPrice p in seedWindow) seed += p.Close;
        seed /= Period;
        results.Add(seed);

        // EMA formula: (close - prevEma) * multiplier + prevEma
        for (int i = Period; i < arr.Length; i++)
        {
            decimal ema = (arr[i].Close - results[^1]) * multiplier + results[^1];
            results.Add(ema);
        }

        return results;
    }

    /// <summary>
    /// Signal: Golden cross (SMA20 crosses above SMA50) = Buy.
    /// Death cross (SMA20 crosses below SMA50) = Sell.
    /// Requires at least 51 data points.
    /// </summary>
    public override bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal)
    {
        signal = SignalType.None;
        if (prices.Count < 51) return false;

        // Calculate SMA20 and SMA50 for the last two days
        var sma20 = _sma20.Calculate(prices);
        var sma50 = _sma50.Calculate(prices);

        // Today and yesterday SMA20 values
        decimal sma20Today     = sma20[^1];
        decimal sma20Yesterday = sma20[^2];

        // Today and yesterday SMA50 values
        decimal sma50Today     = sma50[^1];
        decimal sma50Yesterday = sma50[^2];

        bool goldenCross = sma20Yesterday <= sma50Yesterday && sma20Today > sma50Today;
        bool deathCross  = sma20Yesterday >= sma50Yesterday && sma20Today < sma50Today;

        signal = (goldenCross, deathCross) switch
        {
            (true, _) => SignalType.Buy,
            (_, true) => SignalType.Sell,
            _         => SignalType.None
        };

        return signal != SignalType.None;
    }
}
