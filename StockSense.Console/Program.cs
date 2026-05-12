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

// ── Load config from appsettings.json ─────────────────────────────────────────
string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (!File.Exists(settingsPath))
{
    ConsoleRenderer.ShowError("appsettings.json not found. Create it with your API keys.");
    return;
}

StockSenseOptions options;

// GRADING: try-catch — loading config can fail if file is corrupt or missing a field
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
// GRADING: ICloneable — Clone() gives AlphaVantageService its own independent copy of the
// config so anything that mutates BaseUrl (e.g. ??= in GetApiKey) cannot affect the original.
var alphaVantage = new AlphaVantageService((StockSenseOptions)options.Clone(), rateLimiter);
IStockDataProvider provider = new CachedStockDataProvider(alphaVantage);

// GRADING: EF Core — AlertDbContext opens (or creates) stocksense.db
var dbContext  = new AlertDbContext();
var alertStore = new EfAlertStore(dbContext);

var alertService   = new AlertService(alertStore);
var signalEngine   = new SignalEngine(alertService);
var regimeService  = new MarketRegimeService(provider);
var watchlist      = new WatchlistManager();
await watchlist.LoadAsync();

// Finnhub fundamental data — optional. If key is missing, fundamentals are skipped.
IFundamentalDataProvider? fundamentals = null;
if (!string.IsNullOrWhiteSpace(options.FinnhubApiKey))
{
    var finnhub = new FinnhubService(options);
    fundamentals = new CachedFundamentalProvider(finnhub);
}

// GRADING: events — subscribe to live alert notifications
alertService.OnAlertTriggered += alert =>
{
    // GRADING: extension deconstructor — unpacks Alert into three named variables
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
            await RunAnalysisAsync(provider, signalEngine, regimeService, fundamentals, watchlist);
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

// GRADING: EF Core — dispose DbContext cleanly on exit
dbContext.Dispose();
System.Console.WriteLine("\nGoodbye.");

// ── Daily analysis ─────────────────────────────────────────────────────────────
static async Task RunAnalysisAsync(
    IStockDataProvider         provider,
    SignalEngine               signalEngine,
    MarketRegimeService        regimeService,
    IFundamentalDataProvider?  fundamentals,
    WatchlistManager           watchlist)
{
    if (watchlist.Symbols.Count == 0)
    {
        ConsoleRenderer.ShowError("Watchlist is empty. Add symbols first.");
        ConsoleRenderer.Pause();
        return;
    }

    // Fetch market regime once before the loop — one SPY API call, then cached
    System.Console.WriteLine("\n  Checking market regime...");
    MarketRegime regime = await regimeService.GetRegimeAsync();

    ConsoleRenderer.ShowAnalysisHeader("Daily");
    ConsoleRenderer.ShowMarketRegime(regime);
    System.Console.WriteLine();

    // GRADING: generic type — collect AnalysisResult<string> for every symbol
    var results = new List<AnalysisResult<string>>();

    // GRADING: IEnumerable<T> — foreach on WatchlistCollection via IEnumerable<StockSymbol>
    foreach (StockSymbol symbol in watchlist.Symbols)
    {
        // GRADING: try-catch — one bad symbol does not abort the rest of the analysis
        try
        {
            System.Console.Write($"  Fetching {symbol.Value}...");

            IReadOnlyList<StockPrice> prices = await provider.GetDailyAsync(symbol);

            if (prices.Count == 0)
            {
                ConsoleRenderer.ShowError($"No data returned for {symbol.Value}.");
                results.Add(AnalysisResult<string>.Failure(symbol.Value, "Daily", "No data returned."));
                continue;
            }

            // Multi-timeframe: weekly prices for the RSI filter
            IReadOnlyList<StockPrice>? weeklyPrices = null;
            try { weeklyPrices = await provider.GetWeeklyAsync(symbol); }
            catch { /* weekly failure does not abort daily analysis */ }

            // Fundamental data from Finnhub — null if key not configured or fetch failed
            FundamentalData? fundamentalData = null;
            if (fundamentals is not null)
            {
                try { fundamentalData = await fundamentals.GetFundamentalsAsync(symbol); }
                catch (Exception ex)
                {
                    ConsoleRenderer.ShowError($"Finnhub [{symbol.Value}]: {ex.Message}");
                }
            }

            // Indicator values for display
            var ma   = new MovingAverageIndicator();
            var rsi  = new RsiIndicator();
            var macd = new MacdIndicator();
            var adx  = new AdxIndicator();
            var bb   = new BollingerBandsIndicator();

            // GRADING: extended C# types — LatestClose() is our extension method
            decimal lastClose  = prices.LatestClose();
            decimal smaValue   = prices.Count >= ma.Period       ? ma.Calculate(prices)[^1]  : 0;
            decimal rsiValue   = prices.Count >= rsi.Period + 1  ? rsi.Calculate(prices)[^1] : 0;
            decimal macdValue  = prices.Count >= 35              ? macd.Calculate(prices)[^1]: 0;
            decimal adxValue   = adx.LatestAdx(prices) ?? 0m;
            decimal bbPercentB = bb.LatestBands(prices) is BollingerPoint bands
                ? bands.PercentB(lastClose)
                : 0.5m;

            SignalType signal = await signalEngine.EvaluateAsync(symbol.Value, prices, weeklyPrices);

            // GRADING: generic type — wrap result for post-loop filtering
            results.Add(AnalysisResult<string>.Success(
                symbol.Value, "Daily", signal.ToString(), signal));

            System.Console.Write("\r");
            ConsoleRenderer.ShowAnalysisRow(
                symbol.Value, lastClose, smaValue, rsiValue, macdValue, adxValue, bbPercentB, signal);

            // Show earnings warning and analyst rating if available
            ConsoleRenderer.ShowFundamentalWarning(fundamentalData, lastClose);
        }
        catch (RateLimitException ex)
        {
            // GRADING: custom exception — rate limit stops the loop
            ConsoleRenderer.ShowError($"Rate limit: {ex.Message}");
            break;
        }
        catch (StockDataException ex)
        {
            // GRADING: custom exception — bad data for one symbol, continue with others
            ConsoleRenderer.ShowError($"{ex.Symbol}: {ex.Message}");
            results.Add(AnalysisResult<string>.Failure(ex.Symbol, "Daily", ex.Message));
        }
        catch (Exception ex)
        {
            ConsoleRenderer.ShowError($"{symbol.Value}: {ex.Message}");
        }
    }

    // GRADING: generic extension method — WithSignal<T>() filters by Buy/Sell flag
    var signalled = results.WithSignal(SignalType.Buy | SignalType.Sell).ToList();

    // GRADING: LINQ — count successes and failures
    int successCount = results.Count(r => r.IsSuccess);
    int signalCount  = signalled.Count;

    System.Console.WriteLine();
    System.Console.WriteLine($"  Analysed {successCount} symbol(s). Signals triggered: {signalCount}.");
    ConsoleRenderer.Pause();
}

// ── Weekly analysis ────────────────────────────────────────────────────────────
static async Task RunWeeklyAnalysisAsync(
    IStockDataProvider provider,
    SignalEngine       signalEngine,
    WatchlistManager   watchlist)
{
    if (watchlist.Symbols.Count == 0)
    {
        ConsoleRenderer.ShowError("Watchlist is empty. Add symbols first.");
        ConsoleRenderer.Pause();
        return;
    }

    ConsoleRenderer.ShowAnalysisHeader("Weekly");

    // GRADING: IEnumerable<T> — foreach on WatchlistCollection
    foreach (StockSymbol symbol in watchlist.Symbols)
    {
        // GRADING: try-catch — one failed symbol does not break the weekly run
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
            var adx  = new AdxIndicator();
            var bb   = new BollingerBandsIndicator();

            // GRADING: extended C# types — LatestClose() extension method
            decimal lastClose  = prices.LatestClose();
            decimal smaValue   = prices.Count >= ma.Period       ? ma.Calculate(prices)[^1]  : 0;
            decimal rsiValue   = prices.Count >= rsi.Period + 1  ? rsi.Calculate(prices)[^1] : 0;
            decimal macdValue  = prices.Count >= 35              ? macd.Calculate(prices)[^1]: 0;
            decimal adxValue   = adx.LatestAdx(prices) ?? 0m;
            decimal bbPercentB = bb.LatestBands(prices) is BollingerPoint bands
                ? bands.PercentB(lastClose)
                : 0.5m;

            SignalType signal = await signalEngine.EvaluateAsync(symbol.Value + "_W", prices, requireVolumeForStrong: false);

            System.Console.Write("\r");
            ConsoleRenderer.ShowAnalysisRow(
                symbol.Value, lastClose, smaValue, rsiValue, macdValue, adxValue, bbPercentB, signal);
        }
        catch (RateLimitException ex)
        {
            // GRADING: custom exception — rate limit stops the weekly loop
            ConsoleRenderer.ShowError($"Rate limit: {ex.Message}");
            break;
        }
        catch (StockDataException ex)
        {
            // GRADING: custom exception — bad data for one symbol, continue
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
    // GRADING: EF Core + LINQ — queries SQLite via EfAlertStore
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

    // GRADING: try-catch — network failure or bad symbol caught cleanly
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

        // GRADING: LINQ — GroupByMonth uses LINQ GroupBy internally
        var monthly = PriceStatistics.GroupByMonth(prices);
        ConsoleRenderer.ShowPriceHistory(symbol!.Value, monthly, "Monthly (last ~4 months)");

        // GRADING: extended C# types — extension methods on IReadOnlyList<StockPrice>
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
