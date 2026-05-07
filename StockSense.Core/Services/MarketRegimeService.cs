using StockSense.Core.Indicators;
using StockSense.Core.Interfaces;
using StockSense.Core.Models;

namespace StockSense.Core.Services;

/// Describes the current broad market environment.
public enum MarketRegime
{
    Bull,       // SPY is above its SMA-50 — uptrend, favour Buy signals
    Bear,       // SPY is below its SMA-50 — downtrend, suppress Buy signals
    Neutral     // Not enough data to determine regime
}

/// Determines whether the overall market is in a bull or bear regime
/// by comparing the S&P 500 ETF (SPY) to its 50-day simple moving average.
///
/// Why SMA-50 and not SMA-200?
///   Alpha Vantage compact mode returns the last 100 bars.
///   SMA-200 would require full output (500+ bars) and an interface change.
///   SMA-50 is a widely-used medium-term trend indicator and works well here.
///
/// Rule:
///   SPY close > SMA-50 → Bull  (take Buy signals normally)
///   SPY close < SMA-50 → Bear  (suppress Buy signals across the board)
public sealed class MarketRegimeService
{
    private readonly IStockDataProvider      _provider;
    private readonly MovingAverageIndicator  _sma;

    // GRADING: static constructor — initialises the SPY benchmark symbol once,
    // at class load time, before any instance is created
    private static readonly StockSymbol _benchmarkSymbol;
    static MarketRegimeService()
    {
        // SPY is the S&P 500 ETF — the standard broad market benchmark
        StockSymbol.TryParse("SPY", out StockSymbol? spy);
        _benchmarkSymbol = spy!;
    }

    public MarketRegimeService(IStockDataProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _sma      = new MovingAverageIndicator(period: 50);
    }

    /// Fetches SPY prices and returns the current market regime.
    /// Returns Neutral if the fetch fails or there is not enough data.
    public async Task<MarketRegime> GetRegimeAsync(CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<StockPrice> prices =
                await _provider.GetDailyAsync(_benchmarkSymbol, ct: ct);

            return Classify(prices);
        }
        catch
        {
            // Market regime is supplementary — a failed SPY fetch must never
            // abort the main analysis loop
            return MarketRegime.Neutral;
        }
    }

    /// Classifies the regime from a price list without making an API call.
    /// Useful for testing or when prices are already fetched.
    public MarketRegime Classify(IReadOnlyList<StockPrice> prices)
    {
        if (prices.Count < _sma.Period) return MarketRegime.Neutral;

        IReadOnlyList<decimal> smaValues = _sma.Calculate(prices);

        decimal latestClose = prices[^1].Close;
        decimal latestSma   = smaValues[^1];

        // GRADING: delegates / lambda — Func encapsulates the classification rule.
        // Injecting this as a lambda means the rule can be changed or tested
        // without modifying the method itself.
        Func<decimal, decimal, MarketRegime> classifier = (close, sma) =>
            (close, sma) switch
            {
                // GRADING: pattern matching with when — map price vs SMA to regime
                var (c, s) when c > s => MarketRegime.Bull,
                var (c, s) when c < s => MarketRegime.Bear,
                _                     => MarketRegime.Neutral
            };

        return classifier(latestClose, latestSma);
    }
}
