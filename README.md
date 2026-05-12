# StockSense

A console-based stock market analyser built with **C# 13 / .NET 10**.

Fetches real daily and weekly price data from the Alpha Vantage API, calculates five technical indicators per symbol, generates Buy/Sell signals, saves alerts to a SQLite database, and optionally enriches each symbol with earnings dates and analyst ratings from the Finnhub API.

---

## Table of Contents

- [What it does](#what-it-does)
- [Project structure](#project-structure)
- [Setup](#setup)
- [How to build and run](#how-to-build-and-run)
- [Menu options explained](#menu-options-explained)
- [How signals work](#how-signals-work)
- [Grading requirements](#grading-requirements)
- [Known limitations](#known-limitations)

---

## What it does

1. You build a **watchlist** of stock ticker symbols (e.g. AAPL, MSFT, GOOGL).
2. You run **Analyze Watchlist** (daily) or **Weekly Analysis**.
3. The app fetches price history, calculates five indicators per symbol, combines them into a single signal, and prints a colour-coded results table.
4. Each detected Buy or Sell signal is automatically saved to a **SQLite database** (`stocksense.db`).
5. Optional **Finnhub integration** shows upcoming earnings dates and analyst consensus ratings beneath each symbol's row.
6. A **market regime banner** (Bull / Bear / Neutral) is shown above the table based on whether SPY is above or below its 50-day moving average.
7. You can view the full alert history at any time from the main menu, including the ATR-based entry, stop-loss, and take-profit levels calculated at the time of each alert.
8. The **watchlist persists** between sessions (`watchlist.json`). A built-in **stock directory** of 32 well-known tickers lets you browse and add symbols by sector without typing.

---

## Project structure

The solution is split into two assemblies. All business logic lives in the class library; the console app only handles display and user input.

```
StockSense/
│
├── StockSense.Core/                  ← class library (.dll)
│   ├── Alerts/
│   │   ├── Alert.cs                  — EF Core entity; one saved alert
│   │   └── AlertService.cs           — creates alerts, fires event, delegates to store
│   ├── Collections/
│   │   └── WatchlistCollection.cs    — IEnumerable<T> + custom IEnumerator<T> + events
│   ├── Data/
│   │   ├── AlertDbContext.cs         — EF Core DbContext (SQLite)
│   │   └── EfAlertStore.cs          — IAlertStore backed by EF Core
│   ├── Exceptions/
│   │   └── StockSenseException.cs    — StockDataException, RateLimitException, InvalidSymbolException
│   ├── Extensions/
│   │   ├── AlertExtensions.cs        — extension deconstructor + generic extension methods
│   │   └── StockPriceExtensions.cs   — extension methods on IReadOnlyList<StockPrice>
│   ├── Indicators/
│   │   ├── IndicatorBase.cs          — abstract base for all indicators
│   │   ├── MovingAverageIndicator.cs — SMA + EMA calculation
│   │   ├── RsiIndicator.cs           — Relative Strength Index
│   │   ├── MacdIndicator.cs          — MACD line, signal line, histogram
│   │   ├── BollingerBandsIndicator.cs— upper/middle/lower bands + %B signal
│   │   ├── AdxIndicator.cs           — Average Directional Index (trend strength)
│   │   └── AtrIndicator.cs           — Average True Range (volatility, used for stops)
│   ├── Interfaces/
│   │   ├── IIndicator.cs
│   │   ├── IStockDataProvider.cs
│   │   ├── IAlertStore.cs
│   │   └── IFundamentalDataProvider.cs
│   ├── Models/
│   │   ├── SignalType.cs             — [Flags] enum: None, Buy, Sell, Strong, Weak
│   │   ├── StockPrice.cs             — OHLCV for one bar; IComparable, IEquatable, IFormattable
│   │   ├── StockSymbol.cs            — validated ticker wrapper
│   │   ├── MacdPoint.cs              — MACD / Signal / Histogram for one bar
│   │   ├── BollingerPoint.cs         — Upper / Middle / Lower bands for one bar
│   │   ├── FundamentalData.cs        — earnings date + analyst rating from Finnhub
│   │   ├── AnalysisResult.cs         — generic result wrapper AnalysisResult<T>
│   │   └── IndicatorPoint.cs         — date + decimal value for simple indicators
│   └── Services/
│       ├── StockSenseOptions.cs      — config (ICloneable)
│       ├── RateLimiter.cs            — enforces 5/min and 25/day limits (Queue<T>)
│       ├── AlphaVantageService.cs    — HTTP + JSON parsing for daily/weekly data
│       ├── CachedStockDataProvider.cs— in-memory cache wrapping AlphaVantageService
│       ├── FinnhubService.cs         — earnings calendar + analyst ratings
│       ├── CachedFundamentalProvider.cs — in-memory cache wrapping FinnhubService
│       ├── MarketRegimeService.cs    — SPY vs SMA-50 → Bull / Bear / Neutral
│       ├── PriceStatistics.cs        — GroupByMonth, GroupByYear, AverageVolume
│       ├── JsonAlertStore.cs         — legacy JSON alert store (file-based)
│       ├── AlertService.cs           — static constructor, event, ATR-based trade levels
│       └── SignalEngine.cs           — combines 4 indicators → one signal per symbol
│
└── StockSense.Console/               ← console app (.exe)
    ├── Program.cs                    — wires services, menu loop, analysis functions
    ├── ConsoleRenderer.cs            — all console output (tables, colours, banners)
    ├── WatchlistManager.cs           — manage/persist the watchlist
    ├── StockDirectory.cs             — 32 pre-defined tickers grouped by sector
    └── appsettings.json              ← YOU create this (not in git, see Setup)
```

---

## Setup

### 1. Get a free Alpha Vantage API key

Go to [alphavantage.co/support/#api-key](https://www.alphavantage.co/support/#api-key) and claim a free key (takes under a minute, no credit card required).

Free tier limits: **25 requests per day**, **5 per minute**. The app enforces both automatically.

### 2. (Optional) Get a free Finnhub API key

Go to [finnhub.io/register](https://finnhub.io/register) to get a free key. This unlocks earnings date warnings and analyst consensus ratings in the analysis table. The app works fully without it — the ratings rows are simply skipped.

### 3. Create `appsettings.json`

Create the file at `StockSense.Console/appsettings.json`:

```json
{
  "ApiKey": "YOUR_ALPHA_VANTAGE_KEY",
  "FinnhubApiKey": "YOUR_FINNHUB_KEY",
  "RequestsPerMinute": 5,
  "RequestsPerDay": 25
}
```

- `FinnhubApiKey` is optional — remove or leave empty to skip fundamental data.
- This file is in `.gitignore` and will **never** be committed to version control.

### 4. Verify .NET 10

```bash
dotnet --version   # must show 10.x.x
```

---

## How to build and run

From the `StockSense/` directory:

```bash
# Restore packages and build both projects
dotnet build

# Run the application
dotnet run --project StockSense.Console
```

On the first run, EF Core automatically creates `stocksense.db` in the output directory. No database setup needed.

---

## Menu options explained

```
╔══════════════════════════════╗
║         StockSense           ║
╠══════════════════════════════╣
║  1. Manage watchlist         ║
║  2. Analyze watchlist        ║
║  3. View alert history       ║
║  4. View price history       ║
║  5. Weekly analysis          ║
║  0. Exit                     ║
╚══════════════════════════════╝
```

### Option 1 — Manage watchlist

- **Add symbol manually**: type a ticker (1–5 letters). The app validates it and adds it.
- **Browse stock directory**: a numbered list of 32 tickers grouped by sector (Tech, Finance, Energy, etc.). Enter a number to add it — no typing required.
- **Remove symbol**: shows the current list and prompts for the ticker to remove.
- **View watchlist**: prints all current symbols.

The watchlist is saved to `watchlist.json` after every change and reloaded automatically on startup.

### Option 2 — Analyze watchlist (daily)

For each symbol in the watchlist the app:

1. Fetches the last **100 daily price bars** from Alpha Vantage (cached — subsequent analyses of the same symbol in the same session do not cost an extra API call).
2. Fetches **weekly prices** for the multi-timeframe RSI filter (also cached).
3. Fetches **fundamental data** from Finnhub if a key is configured (also cached).
4. Calculates five indicators: SMA-20, RSI-14, MACD (12/26/9), ADX-14, Bollinger Bands-20.
5. Runs signal logic through `SignalEngine` (see *How signals work* below).
6. Saves any detected signal to the SQLite database as an alert, with ATR-based entry, stop-loss, and take-profit prices.
7. Prints one row in the analysis table.

A **market regime banner** appears above the table showing whether the broad market (SPY) is in a bull or bear trend.

If Finnhub is configured, extra lines are printed beneath each row:
- A yellow ⚠ warning if earnings are within 7 days.
- The analyst consensus rating and price target with upside percentage.

Example output:

```
  ▲ BULL MARKET — SPY is above SMA-50. Conditions favour long trades.

── Daily Analysis ───────────────────────────────────────────────────────
Symbol   Close      SMA-20    RSI     MACD      ADX     BB%B    Signal
─────────────────────────────────────────────────────────────────────────
AAPL     189.30     185.40    42.1    0.8821   28.3   0.54   None
MSFT     415.20     408.60    67.4    3.1240   31.7   0.82   Sell
         Analysts: Buy  Target: $450.00  (+8.4% upside)
```

### Option 3 — View alert history

Reads all saved alerts from `stocksense.db` and prints them newest-first. For each alert, if ATR data was available at the time, the entry price, stop-loss, and take-profit are shown along with the risk/reward ratio.

### Option 4 — View price history

Choose one symbol from your watchlist. The app fetches its daily data and shows a monthly OHLC summary table (last ~4 months) plus the latest close, average close, highest close, and lowest close for the period.

### Option 5 — Weekly analysis

Identical logic to option 2 but uses Alpha Vantage's **weekly price bars** (`TIME_SERIES_WEEKLY`). Weekly signals represent longer-term trends. Alerts are stored with a `_W` suffix (e.g. `AAPL_W`) so they do not collide with daily alerts. The volume confirmation gate for Strong signals is disabled on weekly data — see *How signals work* for why.

---

## How signals work

### The five indicators

**Moving Average (SMA-20)**
Calculates the 20-bar simple moving average. A signal fires when the closing price **crosses** the SMA within the last 3 bars:
- Price crosses above SMA → **Buy**
- Price crosses below SMA → **Sell**

The 3-bar lookback window prevents missing a crossover that happened yesterday but the price hasn't moved far yet today.

**RSI — Relative Strength Index (14-period)**
Measures the speed and magnitude of recent price moves on a scale of 0–100.
- RSI below 30 → stock is oversold, buyers likely to step in → **Buy**
- RSI above 70 → stock is overbought, sellers likely to take profit → **Sell**

**MACD — Moving Average Convergence Divergence (12/26/9)**
- MACD line = 12-period EMA − 26-period EMA
- Signal line = 9-period EMA of the MACD line
- MACD line crosses above signal line → momentum turning bullish → **Buy**
- MACD line crosses below signal line → momentum turning bearish → **Sell**

**Bollinger Bands (20-period, 2 standard deviations)**
Upper band = SMA-20 + 2σ. Lower band = SMA-20 − 2σ.
- Price above the upper band → overbought zone → **Sell**
- Price below the lower band → oversold zone → **Buy**

The BB%B column shows where price sits within the bands: 0.0 = lower band, 0.5 = middle, 1.0 = upper band. Values above 1.0 or below 0.0 mean price is outside the bands.

**ADX — Average Directional Index (14-period)**
ADX does not give Buy/Sell signals — it measures **trend strength** only.
- ADX ≥ 25: market is trending (cyan in table) — signals are reliable.
- ADX 20–24: weakly trending (yellow) — signals are allowed but with lower confidence.
- ADX < 20: market is ranging (grey) — **all signals are suppressed**. RSI and MACD produce many false signals in sideways markets.

### Signal combining

Each of the four signal indicators (MA, RSI, MACD, Bollinger Bands) casts one vote per symbol. The votes are counted in `SignalEngine.CombineSignals()`:

| Votes in same direction | Volume confirmation* | Final signal |
|---|---|---|
| 2 or more Buy votes | Yes | **StrongBuy** |
| 2 or more Sell votes | Yes | **StrongSell** |
| 2 or more Buy votes | No | **Buy** |
| 2 or more Sell votes | No | **Sell** |
| 1 Buy vote | — | **Buy** |
| 1 Sell vote | — | **Sell** |
| 0 votes | — | **None** |

*Volume confirmation: today's volume ≥ 1.5× the 20-bar average. Only applies to daily analysis — weekly analysis does not require volume confirmation because weekly candles aggregate 5 days of volume and rarely spike to 1.5×.

### ADX filter

If ADX < 20 (ranging market), the combined signal is overridden to **None** regardless of what the other indicators say. This prevents acting on false signals in sideways, directionless markets.

### Multi-timeframe RSI filter (daily analysis only)

Before saving a daily Buy signal, the app checks the **weekly RSI** for the same symbol. If the weekly RSI is above 65 — meaning the stock is already approaching overbought on the bigger-picture weekly chart — the daily Buy signal is suppressed. This avoids buying into a stock that may already be extended.

Sell signals are similarly suppressed if the weekly RSI is below 35 (the stock is near oversold on the weekly chart, so a short-term sell may be premature).

### Alert trade levels

When an alert is saved, `AlertService` uses ATR (Average True Range) to calculate suggested trade levels:
- **Entry**: closing price at the time of the signal
- **Stop-loss**: Entry − 1.5 × ATR (for Buy) / Entry + 1.5 × ATR (for Sell)
- **Take-profit**: Entry + 3.0 × ATR (for Buy) / Entry − 3.0 × ATR (for Sell)
- **R/R ratio**: (Target − Entry) / (Entry − Stop) — shown in alert history

---

## Grading requirements

The project satisfies all 14 graded requirements. Each section below explains **where** in the code the requirement lives, **how** it is implemented, and **why** it was used in that way.

---

### 1. `IEnumerable<T>` — 1 point

**File:** `StockSense.Core/Collections/WatchlistCollection.cs`

```csharp
public sealed class WatchlistCollection : IEnumerable<StockSymbol>
{
    public IEnumerator<StockSymbol> GetEnumerator() => new WatchlistEnumerator(_items);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

`WatchlistCollection` is a custom collection that holds the user's watchlist. It implements `IEnumerable<StockSymbol>`, which is the standard .NET contract that says "you can iterate over me with `foreach`".

Two methods are required by the interface:
- The generic `IEnumerator<StockSymbol> GetEnumerator()` — returns our custom enumerator (see requirement 2).
- The non-generic `IEnumerator IEnumerable.GetEnumerator()` — required by older .NET code that doesn't know the element type.

**Why:** Rather than exposing the raw `List<StockSymbol>` directly, wrapping it in a custom collection lets us add an `OnChanged` event (requirement 12), a lazy `Filter()` iterator (requirement 3), and a `Contains()` method — all while still being compatible with `foreach`, LINQ, and any method that accepts `IEnumerable<T>`.

**Where it's called:** `Program.cs` — `foreach (StockSymbol symbol in watchlist.Symbols)` iterates through the watchlist in both the daily and weekly analysis loops.

---

### 2. `IEnumerator<T>` — 1 point

**File:** `StockSense.Core/Collections/WatchlistCollection.cs` (private inner class `WatchlistEnumerator`)

```csharp
private sealed class WatchlistEnumerator : IEnumerator<StockSymbol>
{
    private readonly List<StockSymbol> _source;
    private int _index = -1;

    public StockSymbol Current => _source[_index];
    public bool MoveNext() { _index++; return _index < _source.Count; }
    public void Reset() => _index = -1;
    public void Dispose() { }
}
```

This is a hand-written enumerator — not borrowed from `List<T>`. The `foreach` loop is compiled by C# into a sequence of calls to `GetEnumerator()`, `MoveNext()`, `Current`, and `Dispose()`. This class provides all four.

`_index` starts at −1 (before the first element). Each call to `MoveNext()` advances it by one and returns `true` as long as it is within the list bounds. `Current` returns the element at the current position. `Reset()` moves back to the start.

**Why:** Writing a custom enumerator from scratch demonstrates how `foreach` actually works under the hood, rather than just delegating to `List<T>.GetEnumerator()`.

---

### 3. Iterator (`yield return`) — 0.5 points

**File:** `StockSense.Core/Collections/WatchlistCollection.cs`

```csharp
public IEnumerable<StockSymbol> Filter(Func<StockSymbol, bool> predicate)
{
    foreach (StockSymbol symbol in _items)
        if (predicate(symbol))
            yield return symbol;
}
```

`yield return` turns this method into a **lazy iterator**. Instead of building a new list and returning it all at once, the method returns one matching element at a time — only when the caller asks for the next one. If the caller stops early (e.g. `FirstOrDefault()`), the rest of the list is never visited.

The C# compiler rewrites this method into a hidden state machine class. Each call to `MoveNext()` on the enumerator runs the method body until the next `yield return`, then pauses.

**Why it fits here:** `Filter()` is used in `WatchlistManager.RemoveSymbol()`:

```csharp
StockSymbol? target = _symbols.Filter(s => s.Value == input).FirstOrDefault();
```

Looking for a symbol to remove is literally filtering the collection for a match. `Filter()` lazily yields candidates; `FirstOrDefault()` takes the first one and stops — the rest of the list is never visited.

---

### 4. Extended C# types (extension methods) — 0.5 points

**File:** `StockSense.Core/Extensions/StockPriceExtensions.cs`

```csharp
public static class StockPriceExtensions
{
    public static decimal  LatestClose (this IReadOnlyList<StockPrice> prices) => ...
    public static decimal  AverageClose(this IReadOnlyList<StockPrice> prices) => ...
    public static StockPrice? HighestClose(this IReadOnlyList<StockPrice> prices) => ...
    public static StockPrice? LowestClose (this IReadOnlyList<StockPrice> prices) => ...
    public static IReadOnlyList<StockPrice> InDateRange(this IReadOnlyList<StockPrice> prices, ...) => ...
}
```

Extension methods let you add new methods to types you do not own and cannot modify. `IReadOnlyList<StockPrice>` is a built-in .NET interface — we cannot add `LatestClose()` to it directly. By writing a static class with a static method whose first parameter is `this IReadOnlyList<StockPrice>`, the compiler rewrites `prices.LatestClose()` into `StockPriceExtensions.LatestClose(prices)` automatically.

**Why:** The analysis loop needs the latest closing price, the average, the highest, and the lowest for several display and calculation purposes. Putting these on the list type itself makes the code at the call site (`prices.LatestClose()`) read naturally without requiring a separate utility call.

**Where they're called:** `Program.cs` — in both `RunAnalysisAsync` and `ShowPriceHistoryAsync`:

```csharp
decimal lastClose = prices.LatestClose();
System.Console.WriteLine($"  Average close: ${prices.AverageClose():F2}");
System.Console.WriteLine($"  Highest close: ${prices.HighestClose()?.Close:F2}");
```

---

### 5. Custom exception types — 1 point

**File:** `StockSense.Core/Exceptions/StockSenseException.cs`

```csharp
public class StockSenseException : Exception { ... }

public class StockDataException : StockSenseException
{
    public string Symbol { get; }   // which ticker caused the error
}

public class RateLimitException : StockSenseException { ... }

public class InvalidSymbolException : StockSenseException { ... }
```

Three specific exception types are derived from a common base:
- `StockDataException` — thrown when Alpha Vantage returns unexpected or missing data for a specific ticker. Carries the `Symbol` property so the error message can name the ticker.
- `RateLimitException` — thrown when the daily API quota (25 requests) is exhausted.
- `InvalidSymbolException` — thrown when a user enters a ticker that fails validation (not 1–5 letters).

**Why:** Using specific exception types instead of a bare `Exception` lets the caller handle each failure mode differently. In `Program.cs`:

```csharp
catch (RateLimitException ex)    { break; }       // quota exhausted — stop the loop
catch (StockDataException ex)    { continue; }    // bad symbol — skip it, try the next
catch (Exception ex)             { ... }          // unexpected — log and continue
```

A `RateLimitException` stops the entire analysis loop because there is no point continuing. A `StockDataException` skips only the one bad symbol and lets the rest of the watchlist succeed. This granularity is impossible with a single `catch (Exception)`.

---

### 6. Try-catch blocks — 1 point

**File:** `StockSense.Console/Program.cs`

Four distinct error-handling sites demonstrate different catch strategies:

**a) Config loading** — before the app starts:
```csharp
try { options = JsonSerializer.Deserialize<StockSenseOptions>(json) ?? ...; }
catch (Exception ex) { ConsoleRenderer.ShowError(...); return; }
```
A corrupt or missing `appsettings.json` would crash without this. The error is shown and the app exits cleanly.

**b) Per-symbol daily analysis** — inside the analysis loop:
```csharp
catch (RateLimitException ex) { break; }          // no more quota today
catch (StockDataException  ex) { continue; }      // one bad ticker — skip it
catch (Exception           ex) { continue; }      // unknown error — log and move on
```
One failing symbol never kills the whole run.

**c) Weekly price fetch** — silent catch:
```csharp
try { weeklyPrices = await provider.GetWeeklyAsync(symbol); }
catch { /* weekly failure does not abort daily analysis */ }
```
The weekly prices are only used as a filter. If they can't be fetched, the daily analysis still runs without the multi-timeframe filter.

**d) Finnhub fundamental fetch** — visible error but continues:
```csharp
catch (Exception ex) { ConsoleRenderer.ShowError($"Finnhub [{symbol.Value}]: {ex.Message}"); }
```
Finnhub is optional. If it fails, the error is shown in red and the analysis row is still printed — just without the earnings or rating lines.

---

### 7. Custom generic type — 1 point

**File:** `StockSense.Core/Models/AnalysisResult.cs`

```csharp
public sealed class AnalysisResult<T> where T : class
{
    public string    Symbol      { get; init; }
    public T?        Value       { get; init; }
    public SignalType Signal     { get; init; }
    public bool      IsSuccess   { get; init; }
    public string?   ErrorMessage { get; init; }

    public static AnalysisResult<T> Success(string symbol, string indicator, T value, SignalType signal) => ...
    public static AnalysisResult<T> Failure(string symbol, string indicator, string error) => ...
}
```

`AnalysisResult<T>` wraps the outcome of running an analysis on one stock symbol. `T` is the type of the result value — currently used as `AnalysisResult<string>` where the value is the signal name.

Two static factory methods replace a public constructor: `Success()` creates a result with a value and signal, `Failure()` creates an error result with a message and no value.

**Why:** Without generics, you would need a separate `AnalysisResultString`, `AnalysisResultDecimal`, etc. for each value type. The generic version handles all of them with one class.

**Where it's used:** `Program.cs` — all results from the analysis loop are collected and post-processed:

```csharp
var results = new List<AnalysisResult<string>>();
results.Add(AnalysisResult<string>.Success(symbol.Value, "Daily", signal.ToString(), signal));
int successCount = results.Count(r => r.IsSuccess);
```

---

### 8. Generic type with `where` keyword — 1 point

**File:** `StockSense.Core/Models/AnalysisResult.cs` and `StockSense.Core/Extensions/AlertExtensions.cs`

```csharp
public sealed class AnalysisResult<T> where T : class

public static IEnumerable<AnalysisResult<T>> WithSignal<T>(
    this IEnumerable<AnalysisResult<T>> results,
    SignalType signal)
    where T : class
```

The `where T : class` constraint means `T` must be a reference type (a class), not a value type like `int` or `decimal`.

**Why this constraint is required:** The `Value` property is declared as `T?` (nullable T). The compiler only allows nullable T when it can guarantee T is a reference type — reference types can be null by definition. If T were a value type (like `int`), `T?` would mean `Nullable<int>`, which has different semantics. The constraint enforces this at compile time: attempting `AnalysisResult<int>` would be rejected by the compiler immediately.

---

### 9. Generic extension method — 1 point

**File:** `StockSense.Core/Extensions/AlertExtensions.cs`

```csharp
public static IEnumerable<AnalysisResult<T>> WithSignal<T>(
    this IEnumerable<AnalysisResult<T>> results,
    SignalType signal)
    where T : class
{
    return results.Where(r => (r.Signal & signal) != SignalType.None && r.IsSuccess);
}

public static AnalysisResult<T>? Latest<T>(
    this IEnumerable<AnalysisResult<T>> results)
    where T : class { ... }
```

These are extension methods that are **also generic**. `WithSignal<T>` works on any `IEnumerable<AnalysisResult<T>>` regardless of what `T` is. A non-generic version would only work with `AnalysisResult<string>`, requiring a new copy of the method for every other type.

`WithSignal` uses bitwise AND to check signal flags — `(r.Signal & signal)` allows matching against combined flags like `SignalType.Buy | SignalType.Sell` to catch both directions at once.

**Where it's called:** `Program.cs`:
```csharp
var signalled = results.WithSignal(SignalType.Buy | SignalType.Sell).ToList();
```
This filters the results list down to only symbols that triggered a buy or sell signal — used to print the summary line at the end of the analysis.

---

### 10. Extension deconstructor — 1 point

**File:** `StockSense.Core/Extensions/AlertExtensions.cs`

```csharp
public static void Deconstruct(
    this Alert alert,
    out string    symbol,
    out SignalType signal,
    out string    message)
{
    symbol  = alert.Symbol;
    signal  = alert.Signal;
    message = alert.Message;
}
```

`Alert` has no built-in `Deconstruct` method. This extension method adds tuple-unpacking syntax to `Alert` without modifying `Alert.cs` itself. A deconstructor defined as an extension method (not on the class) is specifically called an **extension deconstructor**.

Once this method exists, the C# compiler allows:
```csharp
var (symbol, signal, message) = alert;
```
instead of writing `alert.Symbol`, `alert.Signal`, `alert.Message` on three separate lines.

**Where it's called:** `Program.cs` — in the event handler subscribed to `OnAlertTriggered`:
```csharp
alertService.OnAlertTriggered += alert =>
{
    var (symbol, signal, message) = alert;
    ConsoleRenderer.ShowSuccess($"  >> ALERT [{signal}] {symbol}: {message}");
};
```

---

### 11. `ICloneable` — 1 point

**File:** `StockSense.Core/Services/StockSenseOptions.cs`

```csharp
public sealed class StockSenseOptions : ICloneable
{
    public object Clone() => new StockSenseOptions
    {
        ApiKey            = ApiKey,
        BaseUrl           = BaseUrl,
        FinnhubApiKey     = FinnhubApiKey,
        FinnhubBaseUrl    = FinnhubBaseUrl,
        RequestsPerMinute = RequestsPerMinute,
        RequestsPerDay    = RequestsPerDay,
    };
}
```

`ICloneable` is a standard .NET interface with a single method `Clone()` that returns an independent copy of the object.

**Why it's needed here:** `StockSenseOptions.GetApiKey()` contains the line `BaseUrl ??= "https://..."` — a mutation that writes back to the object. If the original `options` object were passed directly to `AlphaVantageService`, calling `GetApiKey()` would silently change `BaseUrl` on the shared object, affecting any other code that also holds a reference to `options`. Cloning first gives `AlphaVantageService` its own isolated copy.

**Where it's called:** `Program.cs`:
```csharp
var alphaVantage = new AlphaVantageService((StockSenseOptions)options.Clone(), rateLimiter);
```
The cast to `(StockSenseOptions)` is required because `ICloneable.Clone()` returns `object`.

---

### 12. Events — 1 point

Two separate event implementations exist in the project:

**a) WatchlistCollection — `OnChanged` event**
`StockSense.Core/Collections/WatchlistCollection.cs`

```csharp
public event Action<IReadOnlyList<StockSymbol>>? OnChanged;

public void Add(StockSymbol symbol)
{
    _items.Add(symbol);
    OnChanged?.Invoke(_items);
}
```

Fires whenever a symbol is added or removed. `WatchlistManager` subscribes to it and forwards it through its own `OnChanged` event so that `Program.cs` only needs one subscription point.

**b) AlertService — `OnAlertTriggered` event with custom delegate**
`StockSense.Core/Alerts/AlertService.cs`

```csharp
public delegate void AlertTriggeredHandler(Alert alert);
public event AlertTriggeredHandler? OnAlertTriggered;
```

`AlertService` defines its own named delegate type (`AlertTriggeredHandler`) rather than using `Action<Alert>`. This fires every time a new alert is created and saved — the console can immediately print the alert live as it happens, rather than waiting until the end of the analysis loop.

**Where it's subscribed:** `Program.cs`:
```csharp
alertService.OnAlertTriggered += alert =>
{
    var (symbol, signal, message) = alert;
    ConsoleRenderer.ShowSuccess($"  >> ALERT [{signal}] {symbol}: {message}");
};
```

---

### 13. LINQ — 1 point

LINQ (Language Integrated Query) is used extensively across multiple files, not just as a token usage.

**`StockSense.Core/Data/EfAlertStore.cs`** — querying the SQLite database:
```csharp
List<Alert> existing = await _db.Alerts
    .Where(a => a.Symbol == alert.Symbol)
    .ToListAsync(ct);

Alert? last = existing
    .OrderByDescending(a => a.CreatedAt)
    .FirstOrDefault();
```

**`StockSense.Core/Extensions/AlertExtensions.cs`** — filtering and sorting results:
```csharp
results.Where(r => (r.Signal & signal) != SignalType.None && r.IsSuccess)
results.Where(r => r.IsSuccess).OrderByDescending(r => r.ComputedAt).FirstOrDefault()
```

**`StockSense.Core/Extensions/StockPriceExtensions.cs`** — aggregating price data:
```csharp
prices.Average(p => p.Close)
prices.MaxBy(p => p.Close)
prices.MinBy(p => p.Close)
```

**`StockSense.Core/Services/PriceStatistics.cs`** — grouping prices into monthly summaries:
```csharp
prices.OrderBy(p => p.Date)
      .GroupBy(p => new { p.Date.Year, p.Date.Month })
      .Select(g => new PeriodSummary(...))
      .ToList()
```

**`StockSense.Console/Program.cs`** — post-analysis summary:
```csharp
int successCount = results.Count(r => r.IsSuccess);
var signalled    = results.WithSignal(SignalType.Buy | SignalType.Sell).ToList();
```

---

### 14. Database and Entity Framework — 3 points

**Files:** `StockSense.Core/Data/AlertDbContext.cs`, `StockSense.Core/Data/EfAlertStore.cs`

**NuGet package:** `Microsoft.EntityFrameworkCore.Sqlite 10.0.7` (declared in `StockSense.Core.csproj`)

#### `AlertDbContext`

```csharp
public sealed class AlertDbContext : DbContext
{
    public DbSet<Alert> Alerts { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite("Data Source=stocksense.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Symbol).IsRequired().HasMaxLength(10);
            entity.Property(a => a.Message).IsRequired();
        });
    }
}
```

`DbContext` is the EF Core bridge between C# objects and the database. `DbSet<Alert>` maps to the `Alerts` table. `OnConfiguring` points EF to a local SQLite file — no server installation needed. `OnModelCreating` uses the Fluent API to define the primary key and column constraints.

#### `EfAlertStore`

```csharp
public sealed class EfAlertStore : IAlertStore
{
    public EfAlertStore(AlertDbContext db)
    {
        _db = db;
        _db.Database.EnsureCreated();   // creates stocksense.db and the Alerts table on first run
    }

    public async Task SaveAsync(Alert alert, CancellationToken ct = default)
    {
        List<Alert> existing = await _db.Alerts
            .Where(a => a.Symbol == alert.Symbol)
            .ToListAsync(ct);
        // ... duplicate check ...
        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Alert>> LoadAsync(StockSymbol? symbol = null, ...)
    {
        IQueryable<Alert> query = _db.Alerts;
        if (symbol is not null)
            query = query.Where(a => a.Symbol == symbol.Value);
        List<Alert> alerts = await query.ToListAsync(ct);
        return alerts.OrderByDescending(a => a.CreatedAt).ToList();
    }
}
```

`EnsureCreated()` means the database and table are created automatically on the first run — no migrations or manual SQL needed. The duplicate check runs client-side (in C# after `ToListAsync`) because SQLite cannot translate `DateTimeOffset` comparisons to SQL.

**Where it's used:** `Program.cs`:
```csharp
var dbContext  = new AlertDbContext();
var alertStore = new EfAlertStore(dbContext);
// ... run app ...
dbContext.Dispose();   // cleanly closes the database connection on exit
```

---

## Known limitations

- **Free API quota**: 25 requests/day and 5/minute. Analyzing a 3-symbol watchlist (daily + weekly + SPY regime) uses 7 API calls per run — approximately 3 full runs per day before the quota resets at midnight UTC (3am Lithuanian time).
- **Finnhub ratings may not display**: if no ratings appear despite having a Finnhub key configured, verify the key field name in `appsettings.json` is exactly `"FinnhubApiKey"` (case-sensitive). A red error message will appear if the fetch fails; no message and no ratings indicates the data returned null from the API for that symbol.
- **`None` is the normal daily result**: The MA crossover only fires on the exact day of a crossover. RSI and MACD only fire at extremes. Most symbols on most days will correctly return `None` — this is expected behaviour, not a bug.
- **Console.Clear()** does not fully clear the VS Code integrated terminal scrollback. The app displays correctly in Windows Terminal or `cmd.exe`.
