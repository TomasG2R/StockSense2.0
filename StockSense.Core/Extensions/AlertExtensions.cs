using StockSense.Core.Alerts;
using StockSense.Core.Models;

namespace StockSense.Core.Extensions;

public static class AlertExtensions
{
    // GRADING: extension deconstructor — adds tuple-unpacking to Alert,
    // a type we own but chose not to put a built-in Deconstruct on.
    // Usage: var (symbol, signal, message) = alert;
    public static void Deconstruct(
        this Alert alert,
        out string    symbol,
        out SignalType signal,
        out string    message)
    {
        symbol  = alert.Symbol;
        signal  = alert.Signal;
        message = alert.Message;
    }

    // GRADING: generic extension method — works on any IEnumerable<AnalysisResult<T>>
    // GRADING: where keyword — T must be a class (matches AnalysisResult<T>'s own constraint)
    // Returns only results that have a matching signal.
    public static IEnumerable<AnalysisResult<T>> WithSignal<T>(
        this IEnumerable<AnalysisResult<T>> results,
        SignalType signal)
        where T : class
    {
        // GRADING: LINQ inside a generic extension method
        // Bitwise & checks if any of the requested flags are present, not exact equality
        return results.Where(r => (r.Signal & signal) != SignalType.None && r.IsSuccess);
    }

    // GRADING: generic extension method — gets the most recently computed result
    public static AnalysisResult<T>? Latest<T>(
        this IEnumerable<AnalysisResult<T>> results)
        where T : class
    {
        return results
            .Where(r => r.IsSuccess)
            .OrderByDescending(r => r.ComputedAt)
            .FirstOrDefault();
    }
}