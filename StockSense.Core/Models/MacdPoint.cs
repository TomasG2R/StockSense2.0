namespace StockSense.Core.Models;

/// <summary>
/// Holds the three values produced by a MACD calculation for one date.
/// Macd      = 12-day EMA minus 26-day EMA
/// Signal    = 9-day EMA of the Macd line
/// Histogram = Macd minus Signal  (positive = bullish momentum, negative = bearish)
/// </summary>
public sealed class MacdPoint : IFormattable
{
    public DateTimeOffset Date      { get; init; }
    public decimal        Macd      { get; init; }
    public decimal        Signal    { get; init; }
    public decimal        Histogram { get; init; }

    // ── Deconstructor ────────────────────────────────────────────────────────
    // Lets you write:  var (date, macd, signal, histogram) = point;

    public void Deconstruct(
        out DateTimeOffset date,
        out decimal macd,
        out decimal signal,
        out decimal histogram)
    {
        date      = Date;
        macd      = Macd;
        signal    = Signal;
        histogram = Histogram;
    }

    // ── IFormattable ─────────────────────────────────────────────────────────
    // "S" → short:  2024-03-15 | MACD: 1.23
    // "L" → long:   2024-03-15 | MACD: 1.23  Sig: 0.98  Hist: +0.25
    // null / other  → same as "S"

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        string d = Date.ToString("yyyy-MM-dd");
        string sign = Histogram >= 0 ? "+" : "";

        return format?.ToUpperInvariant() switch
        {
            "L" => $"{d} | MACD: {Macd:F2}  Sig: {Signal:F2}  Hist: {sign}{Histogram:F2}",
            _   => $"{d} | MACD: {Macd:F2}",
        };
    }

    public override string ToString() => ToString("S", null);
}
