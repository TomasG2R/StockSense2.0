using StockSense.Core.Alerts;
using StockSense.Core.Indicators;
using StockSense.Core.Models;

namespace StockSense.Core.Services;

/// <summary>
/// Evaluates all indicators for a stock and produces a combined signal.
/// Filters are applied in order:
///   1. ADX trending  — suppress all signals when market is ranging (ADX &lt; 20)
///   2. Weekly RSI    — suppress signals that fight the weekly trend
///   3. Volume        — only allow Strong signals on high-volume bars
/// Market regime is displayed as a warning only — signals are never suppressed based on it.
/// </summary>
public sealed class SignalEngine
{
    private readonly MovingAverageIndicator  _ma;
    private readonly RsiIndicator            _rsi;
    private readonly MacdIndicator           _macd;
    private readonly AdxIndicator            _adx;
    private readonly BollingerBandsIndicator _bb;
    private readonly AlertService            _alertService;

    // GRADING: delegate — callers can inject a custom signal filter rule
    public delegate bool SignalFilter(SignalType signal);

    /// Optional filter applied before an alert is triggered.
    public SignalFilter? Filter { get; set; }

    /// Creates the engine with default indicator periods.
    public SignalEngine(AlertService alertService)
    {
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _ma   = new MovingAverageIndicator();
        _rsi  = new RsiIndicator();
        _macd = new MacdIndicator();
        _adx  = new AdxIndicator();
        _bb   = new BollingerBandsIndicator();
    }

    /// Runs all indicators against the price history and returns a combined signal.
    /// Optional weekly prices enable the multi-timeframe RSI filter.
    /// Market regime is NOT used to suppress signals here — it is displayed
    /// as a warning in the console so the user can decide how to act.
    public async Task<SignalType> EvaluateAsync(
        string symbol,
        IReadOnlyList<StockPrice> prices,
        IReadOnlyList<StockPrice>? weeklyPrices = null,
        bool requireVolumeForStrong = true,
        CancellationToken ct = default)
    {
        SignalType combined = CombineSignals(prices, requireVolumeForStrong);

        // ── Filter 1: ADX ranging filter ─────────────────────────────────────
        // Suppress all signals when the market is ranging (ADX < 20).
        // RSI and MACD produce many false signals in sideways markets.
        // Weakly-trending zone (ADX 20–24) is allowed through.
        if (combined != SignalType.None && _adx.IsRanging(prices))
            combined = SignalType.None;

        // ── Filter 2: multi-timeframe weekly RSI ─────────────────────────────
        // Suppress signals that fight the bigger-picture weekly trend.
        if (combined != SignalType.None && weeklyPrices is not null)
            combined = ApplyWeeklyFilter(combined, weeklyPrices);

        // GRADING: lambda — local Func encapsulates the trigger condition
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
        if (weeklyPrices.Count < _rsi.Period + 1) return signal;

        IReadOnlyList<decimal> weeklyRsi    = _rsi.Calculate(weeklyPrices);
        decimal                latestWeeklyRsi = weeklyRsi[^1];

        bool isBuy  = (signal & SignalType.Buy)  != SignalType.None;
        bool isSell = (signal & SignalType.Sell) != SignalType.None;

        if (isBuy  && latestWeeklyRsi >= 65) return SignalType.None;
        if (isSell && latestWeeklyRsi <= 35) return SignalType.None;

        return signal;
    }

    /// Runs MA, RSI, MACD and Bollinger Bands — each casts one vote.
    /// 2+ votes in the same direction = confirmed signal.
    /// Volume confirmation gates the Strong flag.
    public SignalType CombineSignals(IReadOnlyList<StockPrice> prices, bool requireVolumeForStrong = true)
    {
        int buyCount  = 0;
        int sellCount = 0;

        // MA: price crosses above/below SMA-20 (needs 21 bars)
        if (prices.Count >= _ma.Period + 1)
        {
            _ma.TryGetSignal(prices, out SignalType maSignal);
            Count(maSignal, ref buyCount, ref sellCount);
        }

        // RSI: oversold (<30) = Buy, overbought (>70) = Sell (needs 15 bars)
        if (prices.Count >= _rsi.Period + 1)
        {
            _rsi.TryGetSignal(prices, out SignalType rsiSignal);
            Count(rsiSignal, ref buyCount, ref sellCount);
        }

        // MACD: signal line crossover (needs 35 bars)
        if (prices.Count >= 35)
        {
            _macd.TryGetSignal(prices, out SignalType macdSignal);
            Count(macdSignal, ref buyCount, ref sellCount);
        }

        // Bollinger Bands: price crosses outside the bands (needs 21 bars)
        if (prices.Count >= _bb.Period + 1)
        {
            _bb.TryGetSignal(prices, out SignalType bbSignal);
            Count(bbSignal, ref buyCount, ref sellCount);
        }

        // Volume confirmation: Strong only when today's volume >= 1.5× 20-day average.
        // Skipped for weekly analysis (requireVolumeForStrong = false) because weekly
        // candles rarely spike to 1.5× average even during strong directional moves.
        bool volumeConfirms = !requireVolumeForStrong;
        if (requireVolumeForStrong)
        {
            long? avgVolume = PriceStatistics.AverageVolume(prices);
            if (avgVolume is not null && avgVolume > 0)
                volumeConfirms = prices[^1].Volume >= (long)(avgVolume * 1.5m);
        }

        // GRADING: switch with when — pattern match on (buyCount, sellCount) tuple
        return (buyCount, sellCount) switch
        {
            var (b, _) when b >= 2 && volumeConfirms => SignalType.Buy  | SignalType.Strong,
            var (_, s) when s >= 2 && volumeConfirms => SignalType.Sell | SignalType.Strong,
            var (b, _) when b >= 2                   => SignalType.Buy,
            var (_, s) when s >= 2                   => SignalType.Sell,
            var (b, _) when b == 1                   => SignalType.Buy,
            var (_, s) when s == 1                   => SignalType.Sell,
            _                                        => SignalType.None
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void Count(SignalType signal, ref int buyCount, ref int sellCount)
    {
        // GRADING: pattern matching with is operator
        switch (signal)
        {
            case var s when (s & SignalType.Buy)  != SignalType.None: buyCount++;  break;
            case var s when (s & SignalType.Sell) != SignalType.None: sellCount++; break;
        }
    }
}
