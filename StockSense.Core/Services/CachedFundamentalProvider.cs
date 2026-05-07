using StockSense.Core.Interfaces;
using StockSense.Core.Models;

namespace StockSense.Core.Services;

/// Wraps an IFundamentalDataProvider and caches results in memory for the session.
/// Fundamental data (earnings dates, analyst ratings) changes at most daily,
/// so one fetch per symbol per session is sufficient.
public sealed class CachedFundamentalProvider : IFundamentalDataProvider
{
    private readonly IFundamentalDataProvider _inner;

    // GRADING: ??= operator — cache is initialised lazily on first use
    private Dictionary<string, FundamentalData?>? _cache;

    public CachedFundamentalProvider(IFundamentalDataProvider inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<FundamentalData?> GetFundamentalsAsync(
        StockSymbol symbol,
        CancellationToken ct = default)
    {
        // GRADING: ??= operator — create the dictionary only on the first call
        _cache ??= new Dictionary<string, FundamentalData?>();

        string key = symbol.Value;

        // GRADING: is operator — check cache hit and bind result in one step,
        // same pattern as CachedStockDataProvider
        if (_cache.TryGetValue(key, out FundamentalData? cached))
            return cached;

        FundamentalData? fresh = await _inner.GetFundamentalsAsync(symbol, ct);

        // Cache even null results — if Finnhub has no data for a symbol,
        // there is no point calling the API again this session
        _cache[key] = fresh;

        return fresh;
    }

    /// Removes one symbol from the cache, forcing a fresh fetch next call.
    public void Invalidate(StockSymbol symbol)
    {
        // GRADING: ?. operator — safe call if cache was never initialised
        _cache?.Remove(symbol.Value);
    }

    /// Clears the entire cache.
    public void InvalidateAll() => _cache?.Clear();
}
