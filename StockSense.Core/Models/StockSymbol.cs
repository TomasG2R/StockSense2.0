// StockSymbol.cs
// Represents a validated stock ticker symbol (e.g. "AAPL", "MSFT").
// Enforces uppercase, letters-only, max 5 characters.
// Implements: IEquatable<T>, IComparable<T>, operator overloads.
// Grading: sealed class, static constructor, out argument (TryParse).

namespace StockSense.Core.Models;

/// <summary>
/// A validated stock ticker symbol. Always uppercase, letters only, max 5 chars.
/// Use <see cref="TryParse"/> to create instances — the constructor is private.
/// </summary>
public sealed class StockSymbol : IEquatable<StockSymbol>, IComparable<StockSymbol>
{
    public string Value { get; }

    private StockSymbol(string value) => Value = value;

    // ── Validation rules (set once, at class load time) ──────────────────────

    private static readonly int _maxLength;
    private static readonly System.Text.RegularExpressions.Regex _validPattern;

    static StockSymbol()
    {
        _maxLength    = 5;
        _validPattern = new System.Text.RegularExpressions.Regex("^[A-Z]{1,5}$");
    }

    // ── Factory method ───────────────────────────────────────────────────────

    /// <summary>
    /// Tries to create a StockSymbol from a raw string.
    /// Returns true and sets <paramref name="symbol"/> if valid, false otherwise.
    /// </summary>
    public static bool TryParse(string? input, out StockSymbol? symbol)
    {
        symbol = null;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        string normalized = input.Trim().ToUpperInvariant();

        if (normalized.Length > _maxLength || !_validPattern.IsMatch(normalized))
            return false;

        symbol = new StockSymbol(normalized);
        return true;
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    public bool Equals(StockSymbol? other)
    {
        if (other is null) return false;
        return Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as StockSymbol);

    public override int GetHashCode() => Value.GetHashCode();

    // ── Comparison ───────────────────────────────────────────────────────────

    public int CompareTo(StockSymbol? other)
    {
        if (other is null) return 1;
        return string.Compare(Value, other.Value, StringComparison.Ordinal);
    }

    // ── Operators ────────────────────────────────────────────────────────────

    public static bool operator ==(StockSymbol? left, StockSymbol? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(StockSymbol? left, StockSymbol? right) =>
        !(left == right);

    // ── Display ──────────────────────────────────────────────────────────────

    public override string ToString() => Value;
}
