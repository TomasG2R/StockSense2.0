using StockSense.Core.Alerts;
using StockSense.Core.Models;
using StockSense.Core.Services;

namespace StockSense.Console;

/// Handles all console output — tables, menus, colored text.
/// No logic lives here, only display.
public static class ConsoleRenderer
{
    // ── Menu ─────────────────────────────────────────────────────────────────

    /// Prints the main menu and returns the user's choice.
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
        System.Console.WriteLine("║  5. Weekly analysis          ║");
        System.Console.WriteLine("║  0. Exit                     ║");
        System.Console.WriteLine("╚══════════════════════════════╝");
        System.Console.Write("\nChoice: ");

        return int.TryParse(System.Console.ReadLine(), out int choice) ? choice : -1;
    }

    // ── Market regime banner ──────────────────────────────────────────────────

    /// Prints a colored market regime banner above the analysis table.
    public static void ShowMarketRegime(MarketRegime regime)
    {
        System.Console.WriteLine();
        switch (regime)
        {
            case MarketRegime.Bull:
                System.Console.ForegroundColor = ConsoleColor.Green;
                System.Console.WriteLine("  ▲ BULL MARKET — SPY is above SMA-50. Conditions favour long trades.");
                break;
            case MarketRegime.Bear:
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine("  ▼ BEAR MARKET — SPY is below SMA-50. Trade carefully, reduce position sizes.");
                break;
            case MarketRegime.Neutral:
                System.Console.ForegroundColor = ConsoleColor.Gray;
                System.Console.WriteLine("  ● MARKET REGIME — Unable to determine (SPY data unavailable).");
                break;
        }
        System.Console.ResetColor();
    }

    // ── Watchlist ─────────────────────────────────────────────────────────────

    /// Prints the current watchlist.
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

    // ── Analysis results ──────────────────────────────────────────────────────

    /// Prints a header row before analysis results.
    public static void ShowAnalysisHeader(string timeframe = "Daily")
    {
        System.Console.WriteLine();
        System.Console.WriteLine($"── {timeframe} Analysis ──────────────────────────────────────────────────────");
        System.Console.WriteLine(
            $"{"Symbol",-8} {"Close",-10} {"SMA-20",-9} {"RSI",-7} {"MACD",-9} {"ADX",-7} {"BB%B",-7} {"Signal",-14}");
        System.Console.WriteLine(new string('─', 75));
    }

    /// Prints one row of analysis results with color-coded signal.
    public static void ShowAnalysisRow(
        string     symbol,
        decimal    close,
        decimal    sma,
        decimal    rsi,
        decimal    macd,
        decimal    adx,
        decimal    bbPercentB,
        SignalType signal)
    {
        System.Console.Write(
            $"{symbol,-8} {close,-10:F2} {sma,-9:F2} {rsi,-7:F1} {macd,-9:F4} ");

        // ADX — color-coded by trend strength
        System.Console.ForegroundColor = adx switch
        {
            >= 25 => ConsoleColor.Cyan,   // trending — signals reliable
            >= 20 => ConsoleColor.Yellow, // weakly trending — caution
            _     => ConsoleColor.Gray    // ranging — signals suppressed
        };
        System.Console.Write($"{adx,-7:F1}");
        System.Console.ResetColor();

        // BB %B — shows where price sits in the bands (0=lower, 1=upper)
        System.Console.Write($"{bbPercentB,-7:F2}");

        // Signal — color-coded
        System.Console.ForegroundColor = signal switch
        {
            var s when (s & SignalType.Buy)  != SignalType.None => ConsoleColor.Green,
            var s when (s & SignalType.Sell) != SignalType.None => ConsoleColor.Red,
            _                                                   => ConsoleColor.Gray
        };
        System.Console.WriteLine($"{signal,-14}");
        System.Console.ResetColor();
    }

    /// Prints a fundamental data warning line beneath a symbol's analysis row.
    /// Only printed when there is something worth showing (earnings soon or analyst rating).
    public static void ShowFundamentalWarning(FundamentalData? data, decimal currentPrice)
    {
        if (data is null) return;

        // Earnings warning — shown when earnings are within 7 days
        if (data.EarningsWithin(7))
        {
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine(
                $"         ⚠ Earnings in {data.DaysUntilEarnings} day(s) — signal may be unreliable.");
            System.Console.ResetColor();
        }

        // Analyst rating — shown when available
        if (data.AnalystRating is not null)
        {
            System.Console.ForegroundColor = data.IsBullishRating ? ConsoleColor.Green : ConsoleColor.Red;
            string targetStr = data.AnalystTarget.HasValue
                ? $"  Target: ${data.AnalystTarget:F2}" +
                  (data.UpsidePotential(currentPrice).HasValue
                      ? $"  ({data.UpsidePotential(currentPrice):+0.0;-0.0}% upside)"
                      : "")
                : "";
            System.Console.WriteLine(
                $"         Analysts: {data.AnalystRating}{targetStr}");
            System.Console.ResetColor();
        }
    }

    // ── Alert history ─────────────────────────────────────────────────────────

    /// Prints a list of historical alerts.
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

            if (a.Entry is not null && a.StopLoss is not null && a.Target is not null)
            {
                decimal rr = a.Entry.Value != a.StopLoss.Value
                    ? Math.Abs(a.Target.Value - a.Entry.Value) /
                      Math.Abs(a.Entry.Value  - a.StopLoss.Value)
                    : 0;

                System.Console.WriteLine($"             Entry:  ${a.Entry:F2}");
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine($"             Stop:   ${a.StopLoss:F2}");
                System.Console.ResetColor();
                System.Console.ForegroundColor = ConsoleColor.Green;
                System.Console.WriteLine($"             Target: ${a.Target:F2}  (R/R {rr:F1})");
                System.Console.ResetColor();
            }
        }
        System.Console.WriteLine();
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    /// Prints an error message in red.
    public static void ShowError(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Red;
        System.Console.WriteLine($"\n  Error: {message}");
        System.Console.ResetColor();
    }

    /// Prints a success message in green.
    public static void ShowSuccess(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine($"\n  {message}");
        System.Console.ResetColor();
    }

    // ── Price history ─────────────────────────────────────────────────────────

    /// Prints a monthly or yearly price history table.
    public static void ShowPriceHistory(
        string symbol,
        IReadOnlyList<PriceStatistics.PeriodSummary> periods,
        string periodLabel)
    {
        System.Console.WriteLine();
        System.Console.WriteLine($"── {symbol} — {periodLabel} History ──────────────────────────────");
        System.Console.WriteLine(
            $"{"Period",-10} {"Open",10} {"High",10} {"Low",10} {"Close",10} {"Change",8} {"Days",5}");
        System.Console.WriteLine(new string('─', 68));

        foreach (var p in periods)
        {
            decimal change = p.Open != 0 ? ((p.Close - p.Open) / p.Open) * 100 : 0;

            System.Console.Write(
                $"{p.Label,-10} {p.Open,10:F2} {p.High,10:F2} {p.Low,10:F2} {p.Close,10:F2} ");

            System.Console.ForegroundColor = change >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
            System.Console.Write($"{change,7:F2}%");
            System.Console.ResetColor();

            System.Console.WriteLine($" {p.TradingDays,5}");
        }
        System.Console.WriteLine();
    }

    /// Waits for the user to press Enter before continuing.
    public static void Pause()
    {
        System.Console.WriteLine("\nPress Enter to continue...");
        System.Console.ReadLine();
    }
}
