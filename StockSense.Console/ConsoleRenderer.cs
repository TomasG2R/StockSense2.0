using StockSense.Core.Alerts;
using StockSense.Core.Models;

namespace StockSense.Console;

/// <summary>
/// Handles all console output — tables, menus, colored text.
/// No logic lives here, only display.
/// </summary>
public static class ConsoleRenderer
{
    // ── Menu ─────────────────────────────────────────────────────────────────

    /// <summary>Prints the main menu and returns the user's choice.</summary>
    public static int ShowMainMenu()
    {
        System.Console.Clear();
        System.Console.WriteLine("╔══════════════════════════════╗");
        System.Console.WriteLine("║         StockSense           ║");
        System.Console.WriteLine("╠══════════════════════════════╣");
        System.Console.WriteLine("║  1. Manage watchlist         ║");
        System.Console.WriteLine("║  2. Analyze watchlist        ║");
        System.Console.WriteLine("║  3. View alert history       ║");
        System.Console.WriteLine("║  4. View price history       ║");
        System.Console.WriteLine("║  0. Exit                     ║");
        System.Console.WriteLine("╚══════════════════════════════╝");
        System.Console.Write("\nChoice: ");

        return int.TryParse(System.Console.ReadLine(), out int choice) ? choice : -1;
    }

    // ── Watchlist ─────────────────────────────────────────────────────────────

    /// <summary>Prints the current watchlist.</summary>
    public static void ShowWatchlist(IReadOnlyList<string> symbols)
    {
        System.Console.WriteLine("\n── Watchlist ──────────────────");
        if (symbols.Count == 0)
        {
            System.Console.WriteLine("  (empty)");
            return;
        }
        foreach (string s in symbols)
            System.Console.WriteLine($"  • {s}");
        System.Console.WriteLine();
    }

    // ── Analysis results ─────────────────────────────────────────────────────

    /// <summary>Prints a header row before analysis results.</summary>
    public static void ShowAnalysisHeader()
    {
        System.Console.WriteLine();
        System.Console.WriteLine($"{"Symbol",-8} {"Last Close",-12} {"SMA-20",-10} {"RSI-14",-10} {"MACD",-10} {"Signal",-12}");
        System.Console.WriteLine(new string('─', 64));
    }

    /// <summary>Prints one row of analysis results with color-coded signal.</summary>
    public static void ShowAnalysisRow(
        string symbol,
        decimal close,
        decimal sma,
        decimal rsi,
        decimal macd,
        SignalType signal)
    {
        System.Console.Write($"{symbol,-8} {close,-12:F2} {sma,-10:F2} {rsi,-10:F2} {macd,-10:F4} ");

        // Color the signal based on Buy/Sell
        System.Console.ForegroundColor = signal switch
        {
            var s when (s & SignalType.Buy)  != SignalType.None => ConsoleColor.Green,
            var s when (s & SignalType.Sell) != SignalType.None => ConsoleColor.Red,
            _                                                   => ConsoleColor.Gray
        };

        System.Console.WriteLine($"{signal,-12}");
        System.Console.ResetColor();
    }

    // ── Alert history ─────────────────────────────────────────────────────────

    /// <summary>Prints a list of historical alerts.</summary>
    public static void ShowAlerts(IReadOnlyList<Alert> alerts)
    {
        System.Console.WriteLine("\n── Alert History ──────────────");
        if (alerts.Count == 0)
        {
            System.Console.WriteLine("  No alerts yet.");
            return;
        }

        foreach (Alert a in alerts)
        {
            System.Console.ForegroundColor = a.IsBuy ? ConsoleColor.Green : ConsoleColor.Red;
            System.Console.Write($"  [{a.CreatedAt:yyyy-MM-dd HH:mm}] {a.Symbol,-8} {a.Signal,-20}");
            System.Console.ResetColor();
            System.Console.WriteLine(a.Message);
        }
        System.Console.WriteLine();
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    /// <summary>Prints an error message in red.</summary>
    public static void ShowError(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Red;
        System.Console.WriteLine($"\n  Error: {message}");
        System.Console.ResetColor();
    }

    /// <summary>Prints a success message in green.</summary>
    public static void ShowSuccess(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine($"\n  {message}");
        System.Console.ResetColor();
    }

    // ── Price history ─────────────────────────────────────────────────────────

    /// <summary>Prints a monthly or yearly price history table.</summary>
    public static void ShowPriceHistory(
        string symbol,
        IReadOnlyList<StockSense.Core.Services.PriceStatistics.PeriodSummary> periods,
        string periodLabel)
    {
        System.Console.WriteLine();
        System.Console.WriteLine($"── {symbol} — {periodLabel} History ──────────────────────────────");
        System.Console.WriteLine($"{"Period",-10} {"Open",10} {"High",10} {"Low",10} {"Close",10} {"Change",8} {"Days",5}");
        System.Console.WriteLine(new string('─', 68));

        foreach (var p in periods)
        {
            decimal change = p.Open != 0 ? ((p.Close - p.Open) / p.Open) * 100 : 0;

            System.Console.Write($"{p.Label,-10} {p.Open,10:F2} {p.High,10:F2} {p.Low,10:F2} {p.Close,10:F2} ");

            System.Console.ForegroundColor = change >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
            System.Console.Write($"{change,7:F2}%");
            System.Console.ResetColor();

            System.Console.WriteLine($" {p.TradingDays,5}");
        }
        System.Console.WriteLine();
    }

    /// <summary>Waits for the user to press Enter before continuing.</summary>
    public static void Pause()
    {
        System.Console.WriteLine("\nPress Enter to continue...");
        System.Console.ReadLine();
    }
}
