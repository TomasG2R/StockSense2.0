using System.Text.Json;
using StockSense.Core.Models;

namespace StockSense.Console;

/// Manages the user's watchlist of stock symbols.
/// Handles adding, removing, and listing symbols.
/// Persists the watchlist to a JSON file between sessions.
public sealed class WatchlistManager
{
    private readonly List<StockSymbol> _symbols = new();
    private readonly string _filePath;
    public WatchlistManager(string filePath = "watchlist.json")
    {
        _filePath = filePath;
    }

    // Delegate: fired whenever the watchlist changes
    public delegate void WatchlistChangedHandler(IReadOnlyList<StockSymbol> current);

    ///Fires when a symbol is added or removed.
    public event WatchlistChangedHandler? OnChanged;

    ///Current symbols as read-only strings for display.
    public IReadOnlyList<string> DisplaySymbols =>
        _symbols.Select(s => s.Value).ToList();

    ///Current symbols as StockSymbol list for analysis.
    public IReadOnlyList<StockSymbol> Symbols => _symbols;

    /// Prompts the user to add a symbol. Validates input via StockSymbol.TryParse.
  
     public async Task AddSymbol()
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
        await SaveAsync();
        ConsoleRenderer.ShowSuccess($"{symbol!.Value} added to watchlist.");
    }

    /// Prompts the user to remove a symbol from the watchlist.
    public async Task RemoveSymbol()
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
        await SaveAsync();
        ConsoleRenderer.ShowSuccess($"{input} removed from watchlist.");
    }

    /// Opens the stock directory so the user can browse and add symbols by number.
    public async Task BrowseDirectory()
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
                        await SaveAsync();
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

    /// Shows the watchlist submenu and handles user input.
    public async Task ManageMenu()
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

            Func<Task> action = input switch
            {
                "1" => AddSymbol,
                "2" => BrowseDirectory,
                "3" => RemoveSymbol,
                "4" => () => { ConsoleRenderer.ShowWatchlist(DisplaySymbols); ConsoleRenderer.Pause(); return Task.CompletedTask; },
                "0" => () => { managing = false; return Task.CompletedTask; },
                _   => () => { ConsoleRenderer.ShowError("Invalid choice."); return Task.CompletedTask; }
            };

            await action();
            if (managing && input != "0") ConsoleRenderer.Pause();
        }
    }

     ///Saves the current watchlist to disk.
    public async Task SaveAsync()
    {
        var symbols = _symbols.Select(s => s.Value).ToList();
        string json = JsonSerializer.Serialize(symbols, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }

    ///Loads the watchlist from disk. Silently skips if file doesn't exist.
    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath)) return;

        string json = await File.ReadAllTextAsync(_filePath);
        var symbols = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

        foreach (string raw in symbols)
        {
            if (StockSymbol.TryParse(raw, out StockSymbol? symbol))
                _symbols.Add(symbol!);
        }
    }
}
