using StockSense.Core.Interfaces;
using StockSense.Core.Models;

namespace StockSense.Core.Services;

/// Wraps an IStockDataProvider and caches results in memory.
/// If prices for a symbol were already fetched today, returns the cached copy.
public sealed class CachedStockDataProvider : IStockDataProvider
{
    private readonly IStockDataProvider _inner;

    // Separate caches for daily and weekly data
    private readonly Dictionary<string, IReadOnlyList<StockPrice>> _dailyCache  = new();
    private readonly Dictionary<string, IReadOnlyList<StockPrice>> _weeklyCache = new();

    /// Creates the provider wrapping any IStockDataProvider implementation.
    public CachedStockDataProvider(IStockDataProvider inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<IReadOnlyList<StockPrice>> GetDailyAsync(
        StockSymbol symbol,
        DateOnly? start = null,
        DateOnly? end   = null,
        CancellationToken ct = default)
    {
        string key = symbol.Value;

        // is operator + pattern matching: check type and bind in one step
        if (_dailyCache.TryGetValue(key, out IReadOnlyList<StockPrice>? cached)
            && cached is List<StockPrice> { Count: > 0 } cachedList)
        {
            return ApplyDateFilter(cachedList, start, end);
        }

        IReadOnlyList<StockPrice> fresh = await _inner.GetDailyAsync(symbol, start, end, ct);

        // Pattern matching with is: confirm the result is usable before caching
        if (fresh is IReadOnlyList<StockPrice> { Count: > 0 })
            _dailyCache[key] = fresh;

        return fresh;
    }

    public async Task<IReadOnlyList<StockPrice>> GetWeeklyAsync(
        StockSymbol symbol,
        CancellationToken ct = default)
    {
        string key = symbol.Value;

        if (_weeklyCache.TryGetValue(key, out IReadOnlyList<StockPrice>? cached)
            && cached is List<StockPrice> { Count: > 0 } cachedList)
        {
            return cachedList;
        }

        IReadOnlyList<StockPrice> fresh = await _inner.GetWeeklyAsync(symbol, ct);

        if (fresh is IReadOnlyList<StockPrice> { Count: > 0 })
            _weeklyCache[key] = fresh;

        return fresh;
    }

    ///Removes a single symbol from both caches, forcing a fresh fetch next time.
    public void Invalidate(StockSymbol symbol)
    {
        _dailyCache.Remove(symbol.Value);
        _weeklyCache.Remove(symbol.Value);
    }

    /// Clears both caches.
    public void InvalidateAll()
    {
        _dailyCache.Clear();
        _weeklyCache.Clear();
    }

    //Private helpers
    private static IReadOnlyList<StockPrice> ApplyDateFilter(
        IReadOnlyList<StockPrice> prices,
        DateOnly? start,
        DateOnly? end)
    {
        IEnumerable<StockPrice> result = prices;

        if (start.HasValue)
            result = result.Where(p => DateOnly.FromDateTime(p.Date.DateTime) >= start.Value);
        if (end.HasValue)
            result = result.Where(p => DateOnly.FromDateTime(p.Date.DateTime) <= end.Value);

        return result.ToList();
    }
}
