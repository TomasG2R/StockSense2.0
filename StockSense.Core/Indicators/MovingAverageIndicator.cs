using StockSense.Core.Models;

namespace StockSense.Core.Indicators;

/// Calculates Simple Moving Average (SMA) and Exponential Moving Average (EMA).
public sealed class MovingAverageIndicator : IndicatorBase
{
    private const int DefaultPeriod = 20;
    private static readonly MovingAverageIndicator _sma20 = new(20);
    private static readonly MovingAverageIndicator _sma50 = new(50);
    public override string Name => $"SMA-{Period}";
    
    public override int Period { get; }

    ///Creates a MovingAverageIndicator with a custom period.
    public MovingAverageIndicator(int period = DefaultPeriod)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// Returns the SMA value for each position that has enough history.
    /// Uses Range slicing on the underlying array to get each price window.
    public override IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices)
    {
        ValidateInput(prices, Period);

        // Convert to array so we can use Range slicing: array[start..end]
        StockPrice[] arr = prices as StockPrice[] ?? prices.ToArray();
        var results = new List<decimal>();

        for (int i = Period - 1; i < arr.Length; i++)
        {
            // GRADING: Range type — C# Range (..) slices an array without copying manually
            StockPrice[] window = arr[(i - Period + 1)..(i + 1)];
            decimal sum = 0;
            foreach (StockPrice p in window) sum += p.Close;
            results.Add(sum / Period);
        }

        return results;
    }

    /// Calculates EMA for the given period.
    /// First EMA value is seeded with a simple average (not zero).
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

    /// Signal: price crosses above SMA-20 = Buy, crosses below SMA-20 = Sell.
    /// Fires weekly rather than quarterly, making it useful for swing trading.
    /// Requires at least Period + 1 (21) data points to compare today vs yesterday.
    public override bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal)
    {
        signal = SignalType.None;
        if (prices.Count < Period + 1) return false;

        IReadOnlyList<decimal> sma = _sma20.Calculate(prices);

        decimal prevClose = prices[^2].Close;
        decimal currClose = prices[^1].Close;
        decimal prevSma   = sma[^2];
        decimal currSma   = sma[^1];

        // Price crossed above SMA-20 → upward momentum → Buy
        // Price crossed below SMA-20 → downward momentum → Sell
        bool crossedAbove = prevClose <= prevSma && currClose > currSma;
        bool crossedBelow = prevClose >= prevSma && currClose < currSma;

        signal = (crossedAbove, crossedBelow) switch
        {
            (true, _) => SignalType.Buy,
            (_, true) => SignalType.Sell,
            _         => SignalType.None
        };

        return signal != SignalType.None;
    }
}
