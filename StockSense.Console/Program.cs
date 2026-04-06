using System.Text.Json;
using StockSense.Core.Alerts;
using StockSense.Core.Indicators;
using StockSense.Core.Interfaces;
using StockSense.Core.Models;
using StockSense.Core.Services;
using StockSense.Console;

// Load API key from appsettings.json
string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (!File.Exists(settingsPath))
{
    ConsoleRenderer.ShowError("appsettings.json not found. Create it with your Alpha Vantage API key.");
    return;
}

StockSenseOptions options;
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

// Wire up services
var rateLimiter   = new RateLimiter(options.RequestsPerMinute, options.RequestsPerDay);
var alphaVantage  = new AlphaVantageService(options, rateLimiter);
IStockDataProvider provider = new CachedStockDataProvider(alphaVantage);

var alertStore    = new JsonAlertStore("alerts.json");
var alertService  = new AlertService(alertStore);
var signalEngine  = new SignalEngine(alertService);
var watchlist     = new WatchlistManager();
await watchlist.LoadAsync();

// Subscribe to alert events — print live when an alert fires
alertService.OnAlertTriggered += alert =>
{
    ConsoleRenderer.ShowSuccess($"ALERT: {alert}");
};

//Main menu loop
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
            await RunAnalysisAsync(provider, signalEngine, watchlist, alertService);
            break;

        case 3:
            await ShowAlertHistoryAsync(alertService);
            break;

        case 4:
            await ShowPriceHistoryAsync(provider, watchlist);
            break;

        case 5:
            await RunWeeklyAnalysisAsync(provider, signalEngine, watchlist, alertService);
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

System.Console.WriteLine("\nGoodbye.");

// Daily analysis 
static async Task RunAnalysisAsync(
    IStockDataProvider provider,
    SignalEngine signalEngine,
    WatchlistManager watchlist,
    AlertService alertService)
{
    if (watchlist.Symbols.Count == 0)
    {
        ConsoleRenderer.ShowError("Watchlist is empty. Add symbols first.");
        ConsoleRenderer.Pause();
        return;
    }

    ConsoleRenderer.ShowAnalysisHeader("Daily");

    foreach (StockSymbol symbol in watchlist.Symbols)
    {
        try
        {
            System.Console.Write($"  Fetching {symbol.Value}...");

            IReadOnlyList<StockPrice> prices = await provider.GetDailyAsync(symbol);

            if (prices.Count == 0)
            {
                ConsoleRenderer.ShowError($"No data returned for {symbol.Value}.");
                continue;
            }

            var ma   = new MovingAverageIndicator();
            var rsi  = new RsiIndicator();
            var macd = new MacdIndicator();

            decimal lastClose = prices[^1].Close;
            decimal smaValue  = prices.Count >= ma.Period  ? ma.Calculate(prices)[^1]   : 0;
            decimal rsiValue  = prices.Count >= rsi.Period + 1 ? rsi.Calculate(prices)[^1] : 0;
            decimal macdValue = prices.Count >= 35         ? macd.Calculate(prices)[^1] : 0;

            SignalType signal = await signalEngine.EvaluateAsync(symbol.Value, prices);

            System.Console.Write("\r");
            ConsoleRenderer.ShowAnalysisRow(symbol.Value, lastClose, smaValue, rsiValue, macdValue, signal);
        }
        catch (Exception ex)
        {
            ConsoleRenderer.ShowError($"{symbol.Value}: {ex.Message}");
        }
    }

    ConsoleRenderer.Pause();
}

//Weekly analysis 
static async Task RunWeeklyAnalysisAsync(
    IStockDataProvider provider,
    SignalEngine signalEngine,
    WatchlistManager watchlist,
    AlertService alertService)
{
    if (watchlist.Symbols.Count == 0)
    {
        ConsoleRenderer.ShowError("Watchlist is empty. Add symbols first.");
        ConsoleRenderer.Pause();
        return;
    }

    ConsoleRenderer.ShowAnalysisHeader("Weekly");

    foreach (StockSymbol symbol in watchlist.Symbols)
    {
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

            decimal lastClose = prices[^1].Close;
            decimal smaValue  = prices.Count >= ma.Period      ? ma.Calculate(prices)[^1]   : 0;
            decimal rsiValue  = prices.Count >= rsi.Period + 1 ? rsi.Calculate(prices)[^1]  : 0;
            decimal macdValue = prices.Count >= 35             ? macd.Calculate(prices)[^1] : 0;

            SignalType signal = await signalEngine.EvaluateAsync(symbol.Value + "_W", prices);

            System.Console.Write("\r");
            ConsoleRenderer.ShowAnalysisRow(symbol.Value, lastClose, smaValue, rsiValue, macdValue, signal);
        }
        catch (Exception ex)
        {
            ConsoleRenderer.ShowError($"{symbol.Value}: {ex.Message}");
        }
    }

    ConsoleRenderer.Pause();
}

//Alert history
static async Task ShowAlertHistoryAsync(AlertService alertService)
{
    var alerts = await alertService.GetHistoryAsync();
    ConsoleRenderer.ShowAlerts(alerts);
    ConsoleRenderer.Pause();
}

//Price history
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

        var monthly = PriceStatistics.GroupByMonth(prices);
        ConsoleRenderer.ShowPriceHistory(symbol!.Value, monthly, "Monthly (last ~4 months)");
    }
    catch (Exception ex)
    {
        ConsoleRenderer.ShowError(ex.Message);
    }

    ConsoleRenderer.Pause();
}
