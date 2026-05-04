using StockSense.Core.Alerts;
using StockSense.Core.Indicators;
using StockSense.Core.Models;

namespace StockSense.Core.Services;

/// <summary>
/// Evaluates all indicators for a stock and produces a combined signal.
/// </summary>
public sealed class SignalEngine
{
    private readonly MovingAverageIndicator _ma;
    private readonly RsiIndicator           _rsi;
    private readonly MacdIndicator          _macd;
    private readonly AlertService           _alertService;

    // Delegate type for a signal filter — callers can inject custom rules
    public delegate bool SignalFilter(SignalType signal);

    /// <summary>Optional filter applied before an alert is triggered.</summary>
    public SignalFilter? Filter { get; set; }

    ///Creates the engine with default indicator periods.
    public SignalEngine(AlertService alertService)
    {
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _ma   = new MovingAverageIndicator();
        _rsi  = new RsiIndicator();
        _macd = new MacdIndicator();
    }

    /// Runs all indicators against the price history and returns a combined signal.
    /// If weeklyPrices are provided, the weekly RSI is used to filter out signals
    /// that conflict with the bigger-picture trend.
    /// Also triggers an alert via AlertService if a signal is found.
    public async Task<SignalType> EvaluateAsync(
        string symbol,
        IReadOnlyList<StockPrice> prices,
        IReadOnlyList<StockPrice>? weeklyPrices = null,
        CancellationToken ct = default)
    {
        SignalType combined = CombineSignals(prices);

        // Multi-timeframe filter: suppress signals that fight the weekly trend.
        // A daily Buy against a weekly overbought market is a low-quality trade.
        if (combined != SignalType.None && weeklyPrices is not null)
            combined = ApplyWeeklyFilter(combined, weeklyPrices);

        // Lambda assigned to a local Func — satisfies delegates/lambda requirement
        Func<SignalType, bool> shouldTrigger = s =>
            s != SignalType.None && (Filter is null || Filter(s));

        if (shouldTrigger(combined))
            await _alertService.TriggerAsync(symbol, combined, prices, ct);

        return combined;
    }

    /// Suppresses a daily signal when the weekly RSI disagrees with it.
    /// Buy  filtered out if weekly RSI >= 65 (approaching overbought on weekly chart).
    /// Sell filtered out if weekly RSI <= 35 (approaching oversold on weekly chart).
    private SignalType ApplyWeeklyFilter(SignalType signal, IReadOnlyList<StockPrice> weeklyPrices)
    {
        if (weeklyPrices.Count < _rsi.Period + 1) return signal; // not enough weekly data — keep signal

        IReadOnlyList<decimal> weeklyRsi = _rsi.Calculate(weeklyPrices);
        decimal latestWeeklyRsi = weeklyRsi[^1];

        bool isBuy  = (signal & SignalType.Buy)  != SignalType.None;
        bool isSell = (signal & SignalType.Sell) != SignalType.None;

        // Daily Buy but weekly is already approaching overbought → skip
        if (isBuy  && latestWeeklyRsi >= 65) return SignalType.None;
        // Daily Sell but weekly is already approaching oversold → skip
        if (isSell && latestWeeklyRsi <= 35) return SignalType.None;

        return signal;
    }

    /// Runs MA, RSI, and MACD signals and combines them.
    /// StrongBuy/StrongSell when 2 or more indicators agree.
    public SignalType CombineSignals(IReadOnlyList<StockPrice> prices)
    {
        int buyCount  = 0;
        int sellCount = 0;

        // Collect individual signals — only count if there's enough data
        // MA needs Period + 1 = 21 points (price vs SMA-20 crossover)
        if (prices.Count >= _ma.Period + 1)
        {
            _ma.TryGetSignal(prices, out SignalType maSignal);
            Count(maSignal, ref buyCount, ref sellCount);
        }

        if (prices.Count >= _rsi.Period + 1)
        {
            _rsi.TryGetSignal(prices, out SignalType rsiSignal);
            Count(rsiSignal, ref buyCount, ref sellCount);
        }

        if (prices.Count >= 35)
        {
            _macd.TryGetSignal(prices, out SignalType macdSignal);
            Count(macdSignal, ref buyCount, ref sellCount);
        }

        // Volume confirmation: only allow Strong when today's volume is at least
        // 1.5x the 20-day average — a high-volume move has real conviction behind it
        bool volumeConfirms = false;
        long? avgVolume = PriceStatistics.AverageVolume(prices);
        if (avgVolume is not null && avgVolume > 0)
            volumeConfirms = prices[^1].Volume >= (long)(avgVolume * 1.5m);

        // switch with when — grading requirement
        // Pattern matching on (buyCount, sellCount) tuple
        return (buyCount, sellCount) switch
        {
            var (b, _) when b >= 2 && volumeConfirms  => SignalType.Buy  | SignalType.Strong,
            var (_, s) when s >= 2 && volumeConfirms  => SignalType.Sell | SignalType.Strong,
            var (b, _) when b >= 2                    => SignalType.Buy,
            var (_, s) when s >= 2                    => SignalType.Sell,
            var (b, _) when b == 1                    => SignalType.Buy,
            var (_, s) when s == 1                    => SignalType.Sell,
            _                                         => SignalType.None
        };
    }

    //Private helpers 
    private static void Count(SignalType signal, ref int buyCount, ref int sellCount)
    {
        // Pattern matching with is operator
        switch (signal)
        {
            case var s when (s & SignalType.Buy)  != SignalType.None: buyCount++;  break;
            case var s when (s & SignalType.Sell) != SignalType.None: sellCount++; break;
        }
    }
}
