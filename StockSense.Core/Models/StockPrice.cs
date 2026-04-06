namespace StockSense.Core.Models;

/// One day of OHLCV price data for a stock.
/// OHLCV = Open, High, Low, Close, Volume.
public sealed class StockPrice
    : IComparable<StockPrice>, IEquatable<StockPrice>, IFormattable
{
    public DateTimeOffset Date  { get; init; }
    public decimal        Open  { get; init; }
    public decimal        High  { get; init; }
    public decimal        Low   { get; init; }
    public decimal        Close { get; init; }
    public long           Volume { get; init; }

    // IComparable<StockPrice
    // Lets you sort a List<StockPrice> chronologically (oldest → newest).
    // Example: prices.Sort() will put Jan 1 before Jan 2.
    public int CompareTo(StockPrice? other)
    {
        if (other is null) return 1;        // nulls sort to the bottom
        return Date.CompareTo(other.Date);
    }

    // IEquatable<StockPrice>
    // Two price records are considered the same if they're from the same calendar day.
    // Useful for duplicate-checking: don't fetch data we already have.
    public bool Equals(StockPrice? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Date.Date == other.Date.Date;
    }

    public override bool Equals(object? obj) => Equals(obj as StockPrice);
    public override int GetHashCode() => Date.Date.GetHashCode();

    // Operator overloading 
    // Lets you write:  if (priceA < priceB)  instead of  priceA.CompareTo(priceB) < 0
    public static bool operator <(StockPrice left, StockPrice right)  => left.CompareTo(right) < 0;
    public static bool operator >(StockPrice left, StockPrice right)  => left.CompareTo(right) > 0;
    public static bool operator ==(StockPrice? left, StockPrice? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(StockPrice? left, StockPrice? right) => !(left == right);

    // Deconstructor 
    // Lets you write:  var (date, close) = price;
    // Handy when looping through prices and you only need those two values.
    public void Deconstruct(out DateTimeOffset date, out decimal close)
    {
        date  = Date;
        close = Close;
    }

    // IFormattable 
    // Controls how the object looks when printed.
    // "S" → short:  2024-03-15 | $182.34
    // "L" → long:   2024-03-15 | O:181.00 H:183.50 L:180.20 C:182.34 V:55,123,400
    // null / other  → same as "S"
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        string d = Date.ToString("yyyy-MM-dd");

        return format?.ToUpperInvariant() switch
        {
            "L" => $"{d} | O:{Open:F2} H:{High:F2} L:{Low:F2} C:{Close:F2} V:{Volume:N0}",
            _   => $"{d} | ${Close:F2}",
        };
    }

    public override string ToString() => ToString("S", null);
}
