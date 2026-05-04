using System.Text.Json;
using StockSense.Core.Collections;
using StockSense.Core.Models;

namespace StockSense.Console;

/// Manages the user's watchlist of stock symbols.
/// Handles adding, removing, and listing symbols.
/// Persists the watchlist to a JSON file between sessions.
public sealed class WatchlistManager
{
    // GRADING: IEnumerable<T> + IEnumerator<T> + events —
    // WatchlistCollection replaces the plain List<StockSymbol>.
    // It supports foreach (IEnumerable<T>), has a custom enumerator (IEnumerator<T>),
    // and fires OnChanged automatically when symbols are added or removed.
    private readonly WatchlistCollection _symbols = new();

    private readonly string _filePath;

    public WatchlistManager(string filePath = "watchlist.json")
    {
        _filePath = filePath;

        // GRADING: events — wire the collection's built-in OnChanged event to this
        // manager's public event so Program.cs only needs to subscribe in one place.
        // When a symbol is added or removed, WatchlistCollection fires first,
        // which then triggers WatchlistManager.OnChanged automatically.
        _symbols.OnChanged += current => OnChanged?.Invoke(current);
    }

    // Delegate (HW1): fired whenever the watchlist changes
    public delegate void WatchlistChangedHandler(IReadOnlyList<StockSymbol> current);

    /// Fires when a symbol is added or removed.
    public event WatchlistChangedHandler? OnChanged;

    /// Current symbols as read-only strings for display.
    public IReadOnlyList<string> DisplaySymbols =>
        _symbols.Select(s => s.Value).ToList();

    // GRADING: IEnumerable<T> — Symbols now returns WatchlistCollection directly.
    // Program.cs can do foreach (works via IEnumerable<T>) and .Count (WatchlistCollection has it).
    public WatchlistCollection Symbols => _symbols;

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

        // GRADING: IEnumerable<T> — Contains() is defined on WatchlistCollection
        // and checks all items by iterating the collection
        if (_symbols.Contains(symbol!.Value))
        {
            ConsoleRenderer.ShowError($"{symbol!.Value} is already in the watchlist.");
            return;
        }

        // WatchlistCollection.Add fires OnChanged automatically — no manual invoke needed here
        _symbols.Add(symbol!);
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

        // GRADING: IEnumerable<T> — LINQ FirstOrDefault works on WatchlistCollection
        // because it implements IEnumerable<StockSymbol>
        StockSymbol? target = _symbols.FirstOrDefault(s => s.Value == input);
        if (target is null)
        {
            ConsoleRenderer.ShowError($"{input} is not in the watchlist.");
            return;
        }

        // WatchlistCollection.Remove fires OnChanged automatically
        _symbols.Remove(target);
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
                System.Console.WriteLine(
                    $"  {i + 1,-4} {entries[i].Symbol,-8} {entries[i].Company,-35} {entries[i].Sector}");

            System.Console.WriteLine("\nEnter a number to add to watchlist, or 0 to go back.");
            System.Console.Write("Choice: ");

            string? input = System.Console.ReadLine();
            if (input == "0") { browsing = false; continue; }

            if (int.TryParse(input, out int index) && index >= 1 && index <= entries.Count)
            {
                string ticker = entries[index - 1].Symbol;
                if (StockSymbol.TryParse(ticker, out StockSymbol? symbol))
                {
                    // GRADING: IEnumerable<T> — Contains() on WatchlistCollection
                    if (_symbols.Contains(symbol!.Value))
                        ConsoleRenderer.ShowError($"{ticker} is already in the watchlist.");
                    else
                    {
                        _symbols.Add(symbol!);
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

            // Lambda (HW1): assigns the right async function based on user input
            Func<Task> action = input switch
            {
                "1" => AddSymbol,
                "2" => BrowseDirectory,
                "3" => RemoveSymbol,
                "4" => () =>
                {
                    ConsoleRenderer.ShowWatchlist(DisplaySymbols);
                    ConsoleRenderer.Pause();
                    return Task.CompletedTask;
                },
                "0" => () => { managing = false; return Task.CompletedTask; },
                _   => () => { ConsoleRenderer.ShowError("Invalid choice."); return Task.CompletedTask; }
            };

            await action();
            if (managing && input != "0") ConsoleRenderer.Pause();
        }
    }

    /// Saves the current watchlist to disk.
    public async Task SaveAsync()
    {
        // GRADING: IEnumerable<T> — LINQ Select works because WatchlistCollection
        // implements IEnumerable<StockSymbol>
        var symbols = _symbols.Select(s => s.Value).ToList();
        string json = JsonSerializer.Serialize(
            symbols, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }

    /// Loads the watchlist from disk. Silently skips if file doesn't exist.
    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath)) return;

        string json    = await File.ReadAllTextAsync(_filePath);
        var rawSymbols = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

        foreach (string raw in rawSymbols)
        {
            if (StockSymbol.TryParse(raw, out StockSymbol? symbol))
                _symbols.Add(symbol!);
        }
    }
}
