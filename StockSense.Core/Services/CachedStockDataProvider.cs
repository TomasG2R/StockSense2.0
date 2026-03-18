  using StockSense.Core.Interfaces;
  using StockSense.Core.Models;

  namespace StockSense.Core.Services;

  /// <summary>
  /// Wraps an IStockDataProvider and caches results in memory.
  /// If prices for a symbol were already fetched today, returns the cached copy.
  /// </summary>
  public sealed class CachedStockDataProvider : IStockDataProvider
  {
      private readonly IStockDataProvider _inner;

      // Dictionary keyed by symbol string → cached price list
      private readonly Dictionary<string, IReadOnlyList<StockPrice>> _cache = new();

      /// <summary>Creates the provider wrapping any IStockDataProvider implementation.</summary>
      public CachedStockDataProvider(IStockDataProvider inner)
      {
          _inner = inner ?? throw new ArgumentNullException(nameof(inner));
      }

      /// <inheritdoc/>
      public async Task<IReadOnlyList<StockPrice>> GetDailyAsync(
          StockSymbol symbol,
          DateOnly? start = null,
          DateOnly? end   = null,
          CancellationToken ct = default)
      {
          string key = symbol.Value;

          // is operator + pattern matching: check type and bind in one step
          if (_cache.TryGetValue(key, out IReadOnlyList<StockPrice>? cached)
              && cached is List<StockPrice> { Count: > 0 } cachedList)
          {
              return ApplyDateFilter(cachedList, start, end);
          }

          IReadOnlyList<StockPrice> fresh = await _inner.GetDailyAsync(symbol, start, end, ct);

          // Pattern matching with is: confirm the result is usable before caching
          if (fresh is IReadOnlyList<StockPrice> { Count: > 0 })
              _cache[key] = fresh;

          return fresh;
      }

      /// <summary>Removes a single symbol from the cache, forcing a fresh fetch next time.</summary>
      public void Invalidate(StockSymbol symbol) =>
          _cache.Remove(symbol.Value);

      /// <summary>Clears the entire cache.</summary>
      public void InvalidateAll() => _cache.Clear();

      // ── Private helpers ───────────────────────────────────────────────────────

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