using StockSense.Core.Models;

namespace StockSense.Core.Interfaces;

// GRADING: custom interface — same pattern as IStockDataProvider and IAlertStore
/// Provides fundamental data (earnings dates, analyst ratings) for a stock symbol.
/// Implemented by FinnhubService; wrapped by CachedFundamentalProvider.
public interface IFundamentalDataProvider
{
    /// Fetches fundamental data for the given symbol.
    /// Returns null if the symbol is unknown or the provider has no data.
    Task<FundamentalData?> GetFundamentalsAsync(
        StockSymbol symbol,
        CancellationToken ct = default);
}
