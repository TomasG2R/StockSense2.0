using StockSense.Core.Alerts;
using StockSense.Core.Models;

namespace StockSense.Core.Interfaces;

public interface IAlertStore
{
    Task SaveAsync(Alert alert, CancellationToken ct = default);

    Task<IReadOnlyList<Alert>> LoadAsync(StockSymbol? symbol = null, CancellationToken ct = default);
}

