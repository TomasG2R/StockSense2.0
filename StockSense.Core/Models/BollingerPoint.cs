namespace StockSense.Core.Models;

/// Holds the three Bollinger Band values for one trading day.
/// Upper  = SMA-20 + (2 × standard deviation)
/// Middle = SMA-20
/// Lower  = SMA-20 - (2 × standard deviation)
public sealed class BollingerPoint : IFormattable
{
    public decimal Upper  { get; init; }
    public decimal Middle { get; init; }
    public decimal Lower  { get; init; }

    /// Bandwidth — how wide the bands are relative to the middle.
    /// High bandwidth = high volatility. Low = squeeze (breakout likely).
    public decimal Bandwidth =>
        Middle != 0 ? (Upper - Lower) / Middle * 100m : 0m;

    /// %B — where the current price sits within the bands.
    /// 0.0 = at lower band, 0.5 = at middle, 1.0 = at upper band.
    /// Above 1.0 or below 0.0 means price is outside the bands.
    public decimal PercentB(decimal price) =>
        (Upper - Lower) != 0 ? (price - Lower) / (Upper - Lower) : 0.5m;

    // GRADING: IFormattable — same pattern as MacdPoint.cs
    // "S" → short:  Upper: 195.20  Mid: 190.10  Lower: 185.00
    // "L" → long:   Upper: 195.20  Mid: 190.10  Lower: 185.00  BW: 5.37%
    // null / other  → same as "S"
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        format?.ToUpperInvariant() switch
        {
            "L" => $"Upper: {Upper:F2}  Mid: {Middle:F2}  Lower: {Lower:F2}  BW: {Bandwidth:F2}%",
            _   => $"Upper: {Upper:F2}  Mid: {Middle:F2}  Lower: {Lower:F2}"
        };

    public override string ToString() => ToString("S", null);
}
