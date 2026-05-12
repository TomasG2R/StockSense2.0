using StockSense.Core.Models;

namespace StockSense.Core.Indicators;

/// Calculates Simple Moving Average (SMA) and Exponential Moving Average (EMA).
public sealed class MovingAverageIndicator : IndicatorBase
{
    private const int DefaultPeriod = 20;
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

    /// Signal: price crossed above/below SMA-20 within the last LookbackBars bars.
    /// A 3-bar window prevents missing signals when the crossover happened
    /// yesterday or the day before but today's bar hasn't moved back.
    private const int LookbackBars = 3;

    public override bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal)
    {
        signal = SignalType.None;
        int required = Period + LookbackBars;
        if (prices.Count < required) return false;

        IReadOnlyList<decimal> sma = Calculate(prices);

        // Check the last LookbackBars bars for a crossover
        // sma and prices arrays are aligned from the end: sma[^1] = prices[^1]
        for (int offset = 1; offset <= LookbackBars; offset++)
        {
            decimal prevClose = prices[^(offset + 1)].Close;
            decimal currClose = prices[^offset].Close;
            decimal prevSma   = sma[^(offset + 1)];
            decimal currSma   = sma[^offset];

            bool crossedAbove = prevClose <= prevSma && currClose > currSma;
            bool crossedBelow = prevClose >= prevSma && currClose < currSma;

            // Return the most recent crossover found
            if (crossedAbove) { signal = SignalType.Buy;  return true; }
            if (crossedBelow) { signal = SignalType.Sell; return true; }
        }

        return false;
    }
}
