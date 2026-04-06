using StockSense.Core.Models;

namespace StockSense.Core.Indicators;

/// Calculates the Relative Strength Index (RSI).
public sealed class RsiIndicator : IndicatorBase
{
    private const int DefaultPeriod = 14;

    public override string Name => $"RSI-{Period}";

    public override int Period { get; }

    /// Creates an RsiIndicator. Period defaults to 14 if not specified.
    /// Named argument example: new RsiIndicator(period: 21)
    public RsiIndicator(int period = DefaultPeriod)
    {
        if (period < 2) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// Returns one RSI value per price point that has enough history.
    /// Needs at least Period + 1 data points.
    public override IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices)
    {
        ValidateInput(prices, Period + 1);

        var results = new List<decimal>();

        // Seed: calculate average gain and average loss over first Period
        decimal avgGain = 0, avgLoss = 0;
        for (int i = 1; i <= Period; i++)
        {
            decimal change = prices[i].Close - prices[i - 1].Close;
            if (change > 0) avgGain += change;
            else            avgLoss += Math.Abs(change);
        }
        avgGain /= Period;
        avgLoss /= Period;

        results.Add(ComputeRsi(avgGain, avgLoss));

        // Subsequent values use Wilder's smoothing
        for (int i = Period + 1; i < prices.Count; i++)
        {
            decimal change = prices[i].Close - prices[i - 1].Close;
            decimal gain = change > 0 ? change : 0;
            decimal loss = change < 0 ? Math.Abs(change) : 0;

            avgGain = (avgGain * (Period - 1) + gain) / Period;
            avgLoss = (avgLoss * (Period - 1) + loss) / Period;

            results.Add(ComputeRsi(avgGain, avgLoss));
        }

        return results;
    }


    /// Signal: Buy if RSI is below 30 (oversold), Sell if above 70 (overbought).
    public override bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal)
    {
        signal = SignalType.None;
        if (prices.Count < Period + 1) return false;

        IReadOnlyList<decimal> rsi = Calculate(prices);
        decimal latest = rsi[^1];

        signal = latest switch
        {
            < 30 => SignalType.Buy,
            > 70 => SignalType.Sell,
            _    => SignalType.None
        };

        return signal != SignalType.None;
    }

    //Private helpers
    private static decimal ComputeRsi(decimal avgGain, decimal avgLoss)
    {
        if (avgLoss == 0) return 100;
        decimal rs = avgGain / avgLoss;
        return 100 - (100 / (1 + rs));
    }
}
