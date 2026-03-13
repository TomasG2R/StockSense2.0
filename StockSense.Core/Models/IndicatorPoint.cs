namespace StockSense.Core.Models;

/// <summary>
/// A single computed indicator value at a specific date.
/// Example: RSI = 72.4 on 2024-03-15.
/// </summary>
public sealed class IndicatorPoint : IEquatable<IndicatorPoint>
{
    public DateTimeOffset Date  { get; init; }
    public decimal        Value { get; init; }

    // ── IEquatable<IndicatorPoint> ───────────────────────────────────────────
    // Two points are equal if they fall on the same date.

    public bool Equals(IndicatorPoint? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Date.Date == other.Date.Date;
    }

    public override bool Equals(object? obj) => Equals(obj as IndicatorPoint);

    public override int GetHashCode() => Date.Date.GetHashCode();

    // ── Deconstructor ────────────────────────────────────────────────────────
    // Lets you write:  var (date, value) = point;

    public void Deconstruct(out DateTimeOffset date, out decimal value)
    {
        date  = Date;
        value = Value;
    }

    public override string ToString() => $"{Date:yyyy-MM-dd} | {Value:F2}";
}
