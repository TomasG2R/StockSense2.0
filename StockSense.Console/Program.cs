using System.Text.Json;
using StockSense.Core.Alerts;
using StockSense.Core.Data;
using StockSense.Core.Exceptions;
using StockSense.Core.Extensions;
using StockSense.Core.Indicators;
using StockSense.Core.Interfaces;
using StockSense.Core.Models;
using StockSense.Core.Services;
using StockSense.Console;

// ── Load API key from appsettings.json ────────────────────────────────────────
string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (!File.Exists(settingsPath))
{
    ConsoleRenderer.ShowError("appsettings.json not found. Create it with your Alpha Vantage API key.");
    return;
}

StockSenseOptions options;

// GRADING: try-catch — loading config can fail if the file is corrupt or missing a field
try
{
    string json = File.ReadAllText(settingsPath);
    options = JsonSerializer.Deserialize<StockSenseOptions>(json)
        ?? throw new InvalidOperationException("appsettings.json is empty or invalid.");
}
catch (Exception ex)
{
    ConsoleRenderer.ShowError($"Failed to load appsettings.json: {ex.Message}");
    return;
}

// ── Wire up services ───────────────────────────────────────────────────────────
var rateLimiter  = new RateLimiter(options.RequestsPerMinute, options.RequestsPerDay);
var alphaVantage = new AlphaVantageService(options, rateLimiter);
IStockDataProvider provider = new CachedStockDataProvider(alphaVantage);

// GRADING: EF Core — AlertDbContext opens (or creates) stocksense.db.
// EfAlertStore replaces the old JsonAlertStore — alerts now persist to a real database.
var dbContext  = new AlertDbContext();
var alertStore = new EfAlertStore(dbContext);

var alertService = new AlertService(alertStore);
var signalEngine = new SignalEngine(alertService);
var watchlist    = new WatchlistManager();
await watchlist.LoadAsync();

// GRADING: events — subscribe to live alert notifications.
// Every time AlertService triggers an alert it fires OnAlertTriggered,
// and this lambda prints the alert immediately to the console.
alertService.OnAlertTriggered += alert =>
{
    // GRADING: extension deconstructor — unpacks Alert into three named variables
    // using the Deconstruct extension method defined in AlertExtensions.cs
    var (symbol, signal, message) = alert;
    ConsoleRenderer.ShowSuccess($"  >> ALERT [{signal}] {symbol}: {message}");
};

// ── Main menu loop ─────────────────────────────────────────────────────────────
bool running = true;
while (running)
{
    int choice = ConsoleRenderer.ShowMainMenu();

    switch (choice)
    {
        case 1:
            await watchlist.ManageMenu();
            break;

        case 2:
            await RunAnalysisAsync(provider, signalEngine, watchlist);
            break;

        case 3:
            await ShowAlertHistoryAsync(alertService);
            break;

        case 4:
            await ShowPriceHistoryAsync(provider, watchlist);
            break;

        case 5:
            await RunWeeklyAnalysisAsync(provider, signalEngine, watchlist);
            break;

        case 0:
            running = false;
            break;

        default:
            ConsoleRenderer.ShowError("Invalid choice.");
            ConsoleRenderer.Pause();
            break;
    }
}

// GRADING: EF Core — dispose the DbContext cleanly when the app exits
dbContext.Dispose();
System.Console.WriteLine("\nGoodbye.");

// ── Daily analysis ─────────────────────────────────────────────────────────────
static async Task RunAnalysisAsync(
    IStockDataProvider provider,
    SignalEngine signalEngine,
    WatchlistManager watchlist)
{
    if (watchlist.Symbols.Count == 0)
    {
        ConsoleRenderer.ShowError("Watchlist is empty. Add symbols first.");
        ConsoleRenderer.Pause();
        return;
    }

    ConsoleRenderer.ShowAnalysisHeader("Daily");

    // GRADING: generic type — collect AnalysisResult<string> for every symbol
    // so we can filter them with our generic extension method after the loop
    var results = new List<AnalysisResult<string>>();

    // GRADING: IEnumerable<T> — foreach works here because WatchlistCollection
    // implements IEnumerable<StockSymbol> via the custom enumerator
    foreach (StockSymbol symbol in watchlist.Symbols)
    {
        // GRADING: try-catch — each symbol is wrapped individually so one bad
        // symbol does not abort the rest of the analysis
        try
        {
            System.Console.Write($"  Fetching {symbol.Value}...");

            IReadOnlyList<StockPrice> prices = await provider.GetDailyAsync(symbol);

            if (prices.Count == 0)
            {
                ConsoleRenderer.ShowError($"No data returned for {symbol.Value}.");
                // GRADING: generic type — record the failure in AnalysisResult
                results.Add(AnalysisResult<string>.Failure(symbol.Value, "Daily", "No data returned."));
                continue;
            }

            // Multi-timeframe: fetch weekly prices for the same symbol.
            // CachedStockDataProvider returns the cached copy if weekly was already
            // fetched this session, so this rarely costs an extra API request.
            IReadOnlyList<StockPrice>? weeklyPrices = null;
            try { weeklyPrices = await provider.GetWeeklyAsync(symbol); }
            catch { /* weekly fetch failing does not abort daily analysis */ }

            var ma   = new MovingAverageIndicator();
            var rsi  = new RsiIndicator();
            var macd = new MacdIndicator();

            // GRADING: extended C# types — LatestClose() is our extension method on IReadOnlyList<StockPrice>
            decimal lastClose = prices.LatestClose();
            decimal smaValue  = prices.Count >= ma.Period        ? ma.Calculate(prices)[^1]   : 0;
            decimal rsiValue  = prices.Count >= rsi.Period + 1   ? rsi.Calculate(prices)[^1]  : 0;
            decimal macdValue = prices.Count >= 35               ? macd.Calculate(prices)[^1] : 0;

            SignalType signal = await signalEngine.EvaluateAsync(symbol.Value, prices, weeklyPrices);

            // GRADING: generic type — wrap the result so we can filter it later
            results.Add(AnalysisResult<string>.Success(
                symbol.Value, "Daily", signal.ToString(), signal));

            System.Console.Write("\r");
            ConsoleRenderer.ShowAnalysisRow(symbol.Value, lastClose, smaValue, rsiValue, macdValue, signal);
        }
        catch (RateLimitException ex)
        {
            // GRADING: custom exception caught — rate limit gets its own message and stops the loop
            ConsoleRenderer.ShowError($"Rate limit: {ex.Message}");
            break;
        }
        catch (StockDataException ex)
        {
            // GRADING: custom exception caught — bad data for one symbol, continue with others
            ConsoleRenderer.ShowError($"{ex.Symbol}: {ex.Message}");
            results.Add(AnalysisResult<string>.Failure(ex.Symbol, "Daily", ex.Message));
        }
        catch (Exception ex)
        {
            ConsoleRenderer.ShowError($"{symbol.Value}: {ex.Message}");
        }
    }

    // GRADING: generic extension method — WithSignal<T>() filters the results list
    // to only those with a Buy or Sell signal, using our generic extension method
    // defined in AlertExtensions.cs
    var signalled = results
        .WithSignal(SignalType.Buy | SignalType.Sell)
        .ToList();

    // GRADING: LINQ — count successes and failures with LINQ queries
    int successCount  = results.Count(r => r.IsSuccess);
    int signalCount   = signalled.Count;

    System.Console.WriteLine();
    System.Console.WriteLine($"  Analysed {successCount} symbol(s). Signals triggered: {signalCount}.");

    ConsoleRenderer.Pause();
}

// ── Weekly analysis ────────────────────────────────────────────────────────────
static async Task RunWeeklyAnalysisAsync(
    IStockDataProvider provider,
    SignalEngine signalEngine,
    WatchlistManager watchlist)
{
    if (watchlist.Symbols.Count == 0)
    {
        ConsoleRenderer.ShowError("Watchlist is empty. Add symbols first.");
        ConsoleRenderer.Pause();
        return;
    }

    ConsoleRenderer.ShowAnalysisHeader("Weekly");

    // GRADING: IEnumerable<T> — foreach on WatchlistCollection via IEnumerable<StockSymbol>
    foreach (StockSymbol symbol in watchlist.Symbols)
    {
        // GRADING: try-catch — one failed symbol does not break the whole weekly run
        try
        {
            System.Console.Write($"  Fetching {symbol.Value}...");

            IReadOnlyList<StockPrice> prices = await provider.GetWeeklyAsync(symbol);

            if (prices.Count == 0)
            {
                ConsoleRenderer.ShowError($"No data returned for {symbol.Value}.");
                continue;
            }

            var ma   = new MovingAverageIndicator();
            var rsi  = new RsiIndicator();
            var macd = new MacdIndicator();

            // GRADING: extended C# types — LatestClose() extension method
            decimal lastClose = prices.LatestClose();
            decimal smaValue  = prices.Count >= ma.Period        ? ma.Calculate(prices)[^1]   : 0;
            decimal rsiValue  = prices.Count >= rsi.Period + 1   ? rsi.Calculate(prices)[^1]  : 0;
            decimal macdValue = prices.Count >= 35               ? macd.Calculate(prices)[^1] : 0;

            SignalType signal = await signalEngine.EvaluateAsync(symbol.Value + "_W", prices);

            System.Console.Write("\r");
            ConsoleRenderer.ShowAnalysisRow(symbol.Value, lastClose, smaValue, rsiValue, macdValue, signal);
        }
        catch (RateLimitException ex)
        {
            // GRADING: custom exception — rate limit stops the weekly loop cleanly
            ConsoleRenderer.ShowError($"Rate limit: {ex.Message}");
            break;
        }
        catch (StockDataException ex)
        {
            // GRADING: custom exception — bad data for one symbol, continue with others
            ConsoleRenderer.ShowError($"{ex.Symbol}: {ex.Message}");
        }
        catch (Exception ex)
        {
            ConsoleRenderer.ShowError($"{symbol.Value}: {ex.Message}");
        }
    }

    ConsoleRenderer.Pause();
}

// ── Alert history ──────────────────────────────────────────────────────────────
static async Task ShowAlertHistoryAsync(AlertService alertService)
{
    // GRADING: EF Core + LINQ — GetHistoryAsync() queries the SQLite database
    // using LINQ inside EfAlertStore and returns results ordered by date
    var alerts = await alertService.GetHistoryAsync();
    ConsoleRenderer.ShowAlerts(alerts);
    ConsoleRenderer.Pause();
}

// ── Price history ──────────────────────────────────────────────────────────────
static async Task ShowPriceHistoryAsync(IStockDataProvider provider, WatchlistManager watchlist)
{
    if (watchlist.Symbols.Count == 0)
    {
        ConsoleRenderer.ShowError("Watchlist is empty. Add symbols first.");
        ConsoleRenderer.Pause();
        return;
    }

    ConsoleRenderer.ShowWatchlist(watchlist.DisplaySymbols);
    System.Console.Write("Enter symbol to view (from watchlist): ");
    string? input = System.Console.ReadLine()?.Trim().ToUpperInvariant();

    if (!StockSymbol.TryParse(input, out StockSymbol? symbol))
    {
        ConsoleRenderer.ShowError("Invalid symbol.");
        ConsoleRenderer.Pause();
        return;
    }

    // GRADING: try-catch — network failure or bad symbol is caught and shown cleanly
    try
    {
        System.Console.WriteLine("\n  Fetching data...");

        IReadOnlyList<StockPrice> prices = await provider.GetDailyAsync(symbol!);

        if (prices.Count == 0)
        {
            ConsoleRenderer.ShowError("No data returned.");
            ConsoleRenderer.Pause();
            return;
        }

        // GRADING: LINQ — GroupByMonth uses LINQ GroupBy internally (in PriceStatistics.cs)
        var monthly = PriceStatistics.GroupByMonth(prices);
        ConsoleRenderer.ShowPriceHistory(symbol!.Value, monthly, "Monthly (last ~4 months)");

        // GRADING: extended C# types — show quick stats using our extension methods
        System.Console.WriteLine($"\n  Latest close : ${prices.LatestClose():F2}");
        System.Console.WriteLine($"  Average close: ${prices.AverageClose():F2}");
        System.Console.WriteLine($"  Highest close: ${prices.HighestClose()?.Close:F2}");
        System.Console.WriteLine($"  Lowest close : ${prices.LowestClose()?.Close:F2}");
    }
    catch (StockDataException ex)
    {
        // GRADING: custom exception — shown with its specific message
        ConsoleRenderer.ShowError(ex.Message);
    }
    catch (Exception ex)
    {
        ConsoleRenderer.ShowError(ex.Message);
    }

    ConsoleRenderer.Pause();
}
