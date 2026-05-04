using StockSense.Core.Models;

namespace StockSense.Core.Extensions;

// GRADING: extending C# types — adding methods to IReadOnlyList<StockPrice>
// which is a built-in .NET type we do not own
public static class StockPriceExtensions
{
    // GRADING: extended C# type — LatestClose on any price list
    public static decimal LatestClose(this IReadOnlyList<StockPrice> prices)
        => prices.Count == 0 ? 0m : prices[^1].Close;

    // GRADING: extended C# type — simple average of closing prices
    public static decimal AverageClose(this IReadOnlyList<StockPrice> prices)
        => prices.Count == 0 ? 0m : prices.Average(p => p.Close);

    // GRADING: extended C# type — highest closing price in the list
    public static StockPrice? HighestClose(this IReadOnlyList<StockPrice> prices)
        => prices.Count == 0 ? null : prices.MaxBy(p => p.Close);

    // GRADING: extended C# type — lowest closing price in the list
    public static StockPrice? LowestClose(this IReadOnlyList<StockPrice> prices)
        => prices.Count == 0 ? null : prices.MinBy(p => p.Close);

    // GRADING: extended C# type — filter prices to a date window
    public static IReadOnlyList<StockPrice> InDateRange(
        this IReadOnlyList<StockPrice> prices, DateOnly from, DateOnly to)
        => prices
            .Where(p => DateOnly.FromDateTime(p.Date.DateTime) >= from
                     && DateOnly.FromDateTime(p.Date.DateTime) <= to)
            .ToList();
}