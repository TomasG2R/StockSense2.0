using StockSense.Core.Models;

namespace StockSense.Core.Interfaces;

public interface IStockDataProvider
{
    Task<IReadOnlyList<StockPrice>> GetDailyAsync(
        StockSymbol symbol,
        DateOnly? start = null,
        DateOnly? end = null,
        CancellationToken ct = default);
}

