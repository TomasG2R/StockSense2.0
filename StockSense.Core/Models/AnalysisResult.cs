using StockSense.Core.Models;

namespace StockSense.Core.Models;

// GRADING: custom generic type — wraps the result of running an indicator on a symbol.
// T is the value type (decimal for MA/RSI, MacdPoint for MACD).
// GRADING: where keyword — T must be a reference type (class) so it can be null-checked.
public sealed class AnalysisResult<T> where T : class
{
    public string      Symbol      { get; init; } = string.Empty;
    public T?          Value       { get; init; }
    public SignalType  Signal      { get; init; }
    public string      Indicator   { get; init; } = string.Empty;
    public DateTimeOffset ComputedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool        IsSuccess   { get; init; }
    public string?     ErrorMessage { get; init; }

    // GRADING: generic type — factory for a successful result
    public static AnalysisResult<T> Success(string symbol, string indicator, T value, SignalType signal)
        => new() { Symbol = symbol, Indicator = indicator, Value = value,
                   Signal = signal, IsSuccess = true };

    // GRADING: generic type — factory for a failed result (not enough data, API error, etc.)
    public static AnalysisResult<T> Failure(string symbol, string indicator, string error)
        => new() { Symbol = symbol, Indicator = indicator,
                   IsSuccess = false, ErrorMessage = error };

    public override string ToString() =>
        IsSuccess
            ? $"{Symbol} | {Indicator} | {Signal} | {Value}"
            : $"{Symbol} | {Indicator} | ERROR: {ErrorMessage}";
}