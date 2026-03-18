using StockSense.Core.Models;

namespace StockSense.Console;

/// <summary>
/// Manages the user's watchlist of stock symbols.
/// Handles adding, removing, and listing symbols.
/// </summary>
public sealed class WatchlistManager
{
    private readonly List<StockSymbol> _symbols = new();

    // Delegate: fired whenever the watchlist changes
    public delegate void WatchlistChangedHandler(IReadOnlyList<StockSymbol> current);

    /// <summary>Fires when a symbol is added or removed.</summary>
    public event WatchlistChangedHandler? OnChanged;

    /// <summary>Current symbols as read-only strings for display.</summary>
    public IReadOnlyList<string> DisplaySymbols =>
        _symbols.Select(s => s.Value).ToList();

    /// <summary>Current symbols as StockSymbol list for analysis.</summary>
    public IReadOnlyList<StockSymbol> Symbols => _symbols;

    /// <summary>
    /// Prompts the user to add a symbol. Validates input via StockSymbol.TryParse.
    /// </summary>
    public void AddSymbol()
    {
        System.Console.Write("\nEnter stock symbol (e.g. AAPL): ");
        string? input = System.Console.ReadLine();

        if (!StockSymbol.TryParse(input, out StockSymbol? symbol))
        {
            ConsoleRenderer.ShowError("Invalid symbol. Must be 1-5 letters (e.g. AAPL).");
            return;
        }

        // Lambda: check for duplicate using Any + lambda predicate
        bool exists = _symbols.Any(s => s.Value == symbol!.Value);
        if (exists)
        {
            ConsoleRenderer.ShowError($"{symbol!.Value} is already in the watchlist.");
            return;
        }

        _symbols.Add(symbol!);
        OnChanged?.Invoke(_symbols);
        ConsoleRenderer.ShowSuccess($"{symbol!.Value} added to watchlist.");
    }

    /// <summary>
    /// Prompts the user to remove a symbol from the watchlist.
    /// </summary>
    public void RemoveSymbol()
    {
        if (_symbols.Count == 0)
        {
            ConsoleRenderer.ShowError("Watchlist is empty.");
            return;
        }

        ConsoleRenderer.ShowWatchlist(DisplaySymbols);
        System.Console.Write("Enter symbol to remove: ");
        string? input = System.Console.ReadLine()?.Trim().ToUpperInvariant();

        // Lambda: find the symbol using FirstOrDefault + lambda predicate
        StockSymbol? target = _symbols.FirstOrDefault(s => s.Value == input);
        if (target is null)
        {
            ConsoleRenderer.ShowError($"{input} is not in the watchlist.");
            return;
        }

        _symbols.Remove(target);
        OnChanged?.Invoke(_symbols);
        ConsoleRenderer.ShowSuccess($"{input} removed from watchlist.");
    }

    /// <summary>
    /// Opens the stock directory so the user can browse and add symbols by number.
    /// </summary>
    public void BrowseDirectory()
    {
        bool browsing = true;
        while (browsing)
        {
            System.Console.Clear();
            System.Console.WriteLine("── Stock Directory ────────────────────────────────────────");
            System.Console.WriteLine($"  {"#",-4} {"Symbol",-8} {"Company",-35} {"Sector"}");
            System.Console.WriteLine(new string('─', 70));

            var entries = StockDirectory.All;
            for (int i = 0; i < entries.Count; i++)
                System.Console.WriteLine($"  {i + 1,-4} {entries[i].Symbol,-8} {entries[i].Company,-35} {entries[i].Sector}");

            System.Console.WriteLine("\nEnter a number to add to watchlist, or 0 to go back.");
            System.Console.Write("Choice: ");

            string? input = System.Console.ReadLine();
            if (input == "0") { browsing = false; continue; }

            if (int.TryParse(input, out int index) && index >= 1 && index <= entries.Count)
            {
                string ticker = entries[index - 1].Symbol;
                if (StockSymbol.TryParse(ticker, out StockSymbol? symbol))
                {
                    bool exists = _symbols.Any(s => s.Value == symbol!.Value);
                    if (exists)
                        ConsoleRenderer.ShowError($"{ticker} is already in the watchlist.");
                    else
                    {
                        _symbols.Add(symbol!);
                        OnChanged?.Invoke(_symbols);
                        ConsoleRenderer.ShowSuccess($"{ticker} added to watchlist.");
                    }
                }
                ConsoleRenderer.Pause();
            }
            else
            {
                ConsoleRenderer.ShowError("Invalid choice.");
                ConsoleRenderer.Pause();
            }
        }
    }

    /// <summary>Shows the watchlist submenu and handles user input.</summary>
    public void ManageMenu()
    {
        bool managing = true;
        while (managing)
        {
            System.Console.Clear();
            System.Console.WriteLine("── Manage Watchlist ───────────");
            System.Console.WriteLine("  1. Add symbol manually");
            System.Console.WriteLine("  2. Browse stock directory");
            System.Console.WriteLine("  3. Remove symbol");
            System.Console.WriteLine("  4. View watchlist");
            System.Console.WriteLine("  0. Back");
            System.Console.Write("\nChoice: ");

            string? input = System.Console.ReadLine();

            Action action = input switch
            {
                "1" => AddSymbol,
                "2" => BrowseDirectory,
                "3" => RemoveSymbol,
                "4" => () => { ConsoleRenderer.ShowWatchlist(DisplaySymbols); ConsoleRenderer.Pause(); },
                "0" => () => { managing = false; },
                _   => () => ConsoleRenderer.ShowError("Invalid choice.")
            };

            action();
            if (managing && input != "0") ConsoleRenderer.Pause();
        }
    }
}
