using StockSense.Core.Models;

namespace StockSense.Core.Services;

/// Aggregates daily price data into monthly or yearly summaries.
public static class PriceStatistics
{
    ///One period's (month or year) aggregated OHLCV data.
    public record PeriodSummary(
        string    Label,
        decimal   Open,
        decimal   High,
        decimal   Low,
        decimal   Close,
        long      Volume,
        int       TradingDays);

    /// Groups daily prices into monthly summaries.
    /// Each month shows open (first day), close (last day), high, low, total volume.
    public static IReadOnlyList<PeriodSummary> GroupByMonth(IReadOnlyList<StockPrice> prices)
    {
        return prices
            .OrderBy(p => p.Date)
            .GroupBy(p => new { p.Date.Year, p.Date.Month })
            .Select(g =>
            {
                var sorted = g.OrderBy(p => p.Date).ToList();
                return new PeriodSummary(
                    Label:       $"{g.Key.Year}-{g.Key.Month:D2}",
                    Open:        sorted.First().Open,
                    High:        sorted.Max(p => p.High),
                    Low:         sorted.Min(p => p.Low),
                    Close:       sorted.Last().Close,
                    Volume:      sorted.Sum(p => p.Volume),
                    TradingDays: sorted.Count);
            })
            .ToList();
    }

    /// Returns the average daily volume over the last <paramref name="period"/> trading days.
    /// Returns null if there are not enough data points.
    public static long? AverageVolume(IReadOnlyList<StockPrice> prices, int period = 20)
    {
        if (prices.Count < period) return null;
        // Take the last `period` prices and average their volume
        return (long)prices
            .Skip(prices.Count - period)
            .Average(p => p.Volume);
    }

    /// Groups daily prices into yearly summaries.
    /// Each year shows open (first day), close (last day), high, low, total volume.
    public static IReadOnlyList<PeriodSummary> GroupByYear(IReadOnlyList<StockPrice> prices)
    {
        return prices
            .OrderBy(p => p.Date)
            .GroupBy(p => p.Date.Year)
            .Select(g =>
            {
                var sorted = g.OrderBy(p => p.Date).ToList();
                decimal open  = sorted.First().Open;
                decimal close = sorted.Last().Close;
                decimal change = open != 0 ? ((close - open) / open) * 100 : 0;

                return new PeriodSummary(
                    Label:       g.Key.ToString(),
                    Open:        open,
                    High:        sorted.Max(p => p.High),
                    Low:         sorted.Min(p => p.Low),
                    Close:       close,
                    Volume:      sorted.Sum(p => p.Volume),
                    TradingDays: sorted.Count);
            })
            .ToList();
    }
}
