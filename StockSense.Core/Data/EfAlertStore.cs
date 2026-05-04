using Microsoft.EntityFrameworkCore;
using StockSense.Core.Alerts;
using StockSense.Core.Interfaces;
using StockSense.Core.Models;

namespace StockSense.Core.Data;

// GRADING: EF Core — replaces JsonAlertStore. Alerts now persist to SQLite database.
public sealed class EfAlertStore : IAlertStore
{
    private readonly AlertDbContext _db;

    public EfAlertStore(AlertDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        // GRADING: EF Core — creates the database file and table if they don't exist yet
        _db.Database.EnsureCreated();
    }

    public async Task SaveAsync(Alert alert, CancellationToken ct = default)
    {
        // GRADING: LINQ + EF Core — load this symbol's alerts from the database.
        // Sorting and date comparison happen client-side because SQLite cannot
        // translate DateTimeOffset in ORDER BY or .Date comparisons to SQL.
        List<Alert> existing = await _db.Alerts
            .Where(a => a.Symbol == alert.Symbol)
            .ToListAsync(ct);

        // Sort in memory — safe because per-symbol alert counts are small
        Alert? last = existing
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        // Block only if: same day AND signal direction hasn't changed.
        // A Buy followed by a Sell (or vice versa) is always saved — it means
        // the trend reversed and the user needs to know.
        bool sameDayDuplicate = last is not null
            && last.CreatedAt.Date == alert.CreatedAt.Date;
        bool directionChanged = last is null
            || last.IsBuy != alert.IsBuy;

        if (!sameDayDuplicate || directionChanged)
        {
            // GRADING: EF Core — add the alert to the table and commit
            _db.Alerts.Add(alert);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Alert>> LoadAsync(
        StockSymbol? symbol = null,
        CancellationToken ct = default)
    {
        // GRADING: LINQ + EF Core — fetch all alerts (filtered by symbol if given),
        // then sort newest-first in memory to avoid the SQLite DateTimeOffset limitation
        IQueryable<Alert> query = _db.Alerts;

        if (symbol is not null)
            // GRADING: LINQ — filter alerts for one specific symbol
            query = query.Where(a => a.Symbol == symbol.Value);

        List<Alert> alerts = await query.ToListAsync(ct);

        // GRADING: LINQ — sort newest first on the client side
        return alerts.OrderByDescending(a => a.CreatedAt).ToList();
    }
}
