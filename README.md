# StockSense

A console-based stock market analyzer built with C# 13 / .NET 10.
Fetches real daily and weekly price data from the Alpha Vantage API, calculates technical indicators (MA, RSI, MACD), generates Buy/Sell signals, and saves alerts to disk.

---

## Table of Contents

- [What it does](#what-it-does)
- [Project structure](#project-structure)
- [Setup](#setup)
- [How to build and run](#how-to-build-and-run)
- [Menu options](#menu-options)
- [How signals work](#how-signals-work)
- [Grading requirements](#grading-requirements)

---

## What it does

1. You build a **watchlist** of stock ticker symbols (e.g. AAPL, MSFT, MCD).
2. You run **Analyze Watchlist** (daily or weekly).
3. The app fetches price history from Alpha Vantage, calculates three indicators for each symbol, combines them into a single signal, and prints a colour-coded results table.
4. If a signal is detected it is automatically saved to `alerts.json`.
5. You can view saved alerts at any time from the main menu.
6. A **stock directory** of 32 well-known tickers (grouped by sector) lets you browse and add symbols without typing.
7. The watchlist survives app restarts — saved to `watchlist.json`.

---

## Project structure

```
StockSense/
├── StockSense.Core/            ← class library (.dll) — all logic
│   ├── Alerts/
│   │   ├── Alert.cs
│   │   └── AlertService.cs
│   ├── Indicators/
│   │   ├── IndicatorBase.cs
│   │   ├── MovingAverageIndicator.cs
│   │   ├── RsiIndicator.cs
│   │   └── MacdIndicator.cs
│   ├── Interfaces/
│   │   ├── IAlertStore.cs
│   │   ├── IIndicator.cs
│   │   └── IStockDataProvider.cs
│   ├── Models/
│   │   ├── IndicatorPoint.cs
│   │   ├── MacdPoint.cs
│   │   ├── SignalType.cs
│   │   ├── StockPrice.cs
│   │   └── StockSymbol.cs
│   └── Services/
│       ├── AlphaVantageService.cs
│       ├── CachedStockDataProvider.cs
│       ├── JsonAlertStore.cs
│       ├── PriceStatistics.cs
│       ├── RateLimiter.cs
│       ├── SignalEngine.cs
│       └── StockSenseOptions.cs
│
└── StockSense.Console/         ← console app (.exe) — UI only
    ├── ConsoleRenderer.cs
    ├── Program.cs
    ├── StockDirectory.cs
    ├── WatchlistManager.cs
    └── appsettings.json        ← YOU create this (not in git)
```

---

## Setup

### 1. Get a free Alpha Vantage API key

Go to https://www.alphavantage.co/support/#api-key and claim a free key (takes 30 seconds, no credit card).

### 2. Create `appsettings.json`

Create the file at `StockSense.Console/appsettings.json` with this content:

```json
{
  "ApiKey": "YOUR_KEY_HERE",
  "BaseUrl": "https://www.alphavantage.co/query",
  "RequestsPerMinute": 5,
  "RequestsPerDay": 25
}
```

Replace `YOUR_KEY_HERE` with the key from step 1.
This file is in `.gitignore` and will never be committed.

> **Free tier limits:** 25 requests per day, 5 per minute.
> The app enforces both limits automatically via `RateLimiter.cs`.

---

## How to build and run

From the `StockSense/` folder:

```bash
# Build both projects
dotnet build

# Run the app
dotnet run --project StockSense.Console
```

The app targets **.NET 10**. Verify your SDK version:

```bash
dotnet --version   # should show 10.x.x
```

---

## Menu options

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

| Option | What it does |
|--------|-------------|
| **1 — Manage watchlist** | Add symbols manually, browse the built-in stock directory by sector, remove symbols, or view the current list. Saved to `watchlist.json` automatically. |
| **2 — Analyze watchlist** | Fetches the last ~100 daily price points for each symbol, calculates SMA-20, RSI-14, and MACD, then prints a colour-coded table (green = Buy, red = Sell). Detected signals are saved to `alerts.json`. |
| **3 — View alert history** | Reads and prints all saved alerts from `alerts.json`, newest first. |
| **4 — View price history** | Pick one symbol and see a monthly summary table (Open, High, Low, Close, % change, trading days) for the last ~4 months. |
| **5 — Weekly analysis** | Same as option 2 but uses weekly price bars (TIME_SERIES_WEEKLY endpoint). Weekly signals represent longer-term trends. Weekly alerts are stored with a `_W` suffix so they don't collide with daily ones. |

---

## How signals work

Each symbol is evaluated by three independent indicators. The results are combined in `SignalEngine.CombineSignals()`:

| Condition | Signal |
|-----------|--------|
| 1 indicator says Buy | Buy |
| 1 indicator says Sell | Sell |
| 2+ indicators say Buy | **StrongBuy** |
| 2+ indicators say Sell | **StrongSell** |
| None triggered | None |

### Moving Average — Golden Cross / Death Cross

- Calculates SMA-20 and SMA-50.
- **Golden cross**: SMA-20 crosses *above* SMA-50 → **Buy**
- **Death cross**: SMA-20 crosses *below* SMA-50 → **Sell**
- Fires only on the exact day of the crossover, so it triggers rarely.

### RSI (Relative Strength Index, 14-period)

- Measures momentum: how fast price is rising or falling.
- RSI < 30 → stock is oversold → **Buy**
- RSI > 70 → stock is overbought → **Sell**

### MACD (Moving Average Convergence Divergence)

- MACD line = 12-period EMA − 26-period EMA
- Signal line = 9-period EMA of the MACD line
- MACD line crosses *above* signal line → **Buy**
- MACD line crosses *below* signal line → **Sell**

---

## Grading requirements

All 21 requirements are implemented. The table below shows exactly where each one lives.

| # | Requirement | Points | File | Line(s) | Implementation detail |
|---|-------------|--------|------|---------|-----------------------|
| 1 | **Custom interface** | 0.5 | `Interfaces/IIndicator.cs` line 6 | `public interface IIndicator` | Three custom interfaces: `IIndicator`, `IStockDataProvider`, `IAlertStore`. Define the contracts that all services implement. |
| 2 | **IComparable\<T\>** | 0.5 | `Models/StockPrice.cs` line 6, 18–22 | `: IComparable<StockPrice>` + `CompareTo()` | Sorts price records chronologically by `Date` so `prices.Sort()` gives oldest-first order. Also in `StockSymbol.cs` line 5. |
| 3 | **IEquatable\<T\>** | 0.5 | `Models/StockPrice.cs` line 6, 27–32 | `: IEquatable<StockPrice>` + `Equals()` | Two `StockPrice` objects are equal if they fall on the same calendar day — used for duplicate detection. Also in `StockSymbol.cs` and `IndicatorPoint.cs`. |
| 4 | **IFormattable** | 1.0 | `Models/StockPrice.cs` line 6, 58–67 and `Models/MacdPoint.cs` line 7, 31–43 | `ToString(string? format, IFormatProvider?)` | Format `"S"` = short (date + close price), `"L"` = long (full OHLCV or full MACD detail). Implemented on both models. |
| 5 | **switch with when** | 0.5 | `Services/SignalEngine.cs` lines 79–86 and 94–97 | `var (b, s) when b >= 2 =>` | Tuple pattern switch selects StrongBuy/StrongSell/Buy/Sell/None based on how many indicators agreed. Classic `switch` with `when` guard in the `Count()` helper. |
| 6 | **Range type** | 0.5 | `Indicators/MovingAverageIndicator.cs` lines 35, 55 | `arr[(i - Period + 1)..(i + 1)]` and `arr[..Period]` | `Range` slicing extracts the exact price window for each SMA period, and seeds the first EMA value. |
| 7 | **Multiple assemblies** | 1.0 | `StockSense.Core.csproj` and `StockSense.Console.csproj` | `<ProjectReference>` in Console csproj | Core is a `.dll` (all logic, indicators, services, models). Console is an `.exe` (display and input only) that references Core. |
| 8 | **sealed class** | 0.5 | `Indicators/MovingAverageIndicator.cs` line 6 | `public sealed class MovingAverageIndicator` | `sealed` means the class cannot be subclassed. Also applied to: `RsiIndicator`, `MacdIndicator`, `StockPrice`, `MacdPoint`, `AlertService`, `WatchlistManager`. |
| 9 | **Abstract class** | 0.5 | `Indicators/IndicatorBase.cs` line 7 | `public abstract class IndicatorBase : IIndicator` | Cannot be instantiated directly. Declares `Name`, `Period`, `Calculate`, `TryGetSignal` as abstract — each concrete indicator must provide its own implementation. |
| 10 | **Static constructor** | 1.0 | `Alerts/AlertService.cs` lines 11–22 | `static AlertService()` | Runs exactly once before `AlertService` is first used. Populates the `MessageTemplates` dictionary with human-readable strings for each `SignalType`. Also in `StockSymbol.cs` line 15 (compiles the validation regex once). |
| 11 | **Deconstructor** | 0.5 | `Models/StockPrice.cs` lines 47–51 | `public void Deconstruct(out DateTimeOffset date, out decimal close)` | Allows `var (date, close) = price;` syntax. Also in `MacdPoint.cs` line 16 (4-value) and `IndicatorPoint.cs` line 26. |
| 12 | **Operator overloading** | 0.5 | `Models/StockPrice.cs` lines 39–42 | `<`, `>`, `==`, `!=` | Allows `if (priceA < priceB)` directly instead of calling `CompareTo`. Also `==`/`!=` in `StockSymbol.cs` lines 59–63. |
| 13 | **System.Collections.Generic** | 1.0 | `Services/RateLimiter.cs` lines 12–13 and `Services/JsonAlertStore.cs` line 14 | `Queue<DateTime>` (×2) and `List<Alert>` | `Queue<DateTime>` (FIFO) tracks when each API request was made — oldest timestamp dequeued first when it expires. `List<Alert>` holds all alerts loaded from disk. `Dictionary<SignalType,string>` in `AlertService`. |
| 14 | **is operator** | 0.5 | `Services/CachedStockDataProvider.cs` lines 32, 40 | `cached is List<StockPrice> { Count: > 0 } cachedList` | Combines type test, property pattern check, and variable binding in a single expression. |
| 15 | **Default and named arguments** | 0.5 | `Indicators/RsiIndicator.cs` line 16 (default) and `Services/PriceStatistics.cs` lines 29–35 (named) | `int period = DefaultPeriod` / `Label: "...", Open: ..., High: ...` | `RsiIndicator(int period = 14)` shows default arguments. `new PeriodSummary(Label: ..., Open: ..., High: ...)` in `PriceStatistics.GroupByMonth` shows named arguments. |
| 16 | **params keyword** | 0.5 | `Indicators/MacdIndicator.cs` line 33 | `params int[] periodOverrides` | `CalculateFull(prices)` uses standard 12/26/9 periods. `CalculateFull(prices, 10, 20, 7)` overrides them. Callers pass zero or more extra arguments without an array literal. |
| 17 | **out arguments** | 1.0 | `Services/AlphaVantageService.cs` lines 34, 67–71 | `out List<StockPrice>? prices, out string? error` | `TryParseResponse()` writes the result into `prices` and any error message into `error` via `out`. The same pattern is used in every `TryGetSignal()` call via `IIndicator`. |
| 18 | **Delegates and lambdas** | 1.5 | `Services/SignalEngine.cs` lines 18, 42; `Console/WatchlistManager.cs` lines 19, 149; `Alerts/AlertService.cs` line 28; `Console/Program.cs` line 42 | `delegate bool SignalFilter`, `Func<SignalType,bool> shouldTrigger = s => ...` | Three custom `delegate` type declarations. `Func<Task>` assigned via lambda in the watchlist menu switch. Lambda subscribed to `OnAlertTriggered` event in `Program.cs`. |
| 19 | **Bitwise operations** | 1.0 | `Models/SignalType.cs` lines 14–17 and `Alerts/Alert.cs` lines 30–32 | `Buy = 1 << 0`, `(Signal & SignalType.Buy) != SignalType.None` | Left-shift (`<<`) defines each flag value as a unique bit. Bitwise AND (`&`) tests whether a flag is set. Bitwise OR (`|`) combines flags: `Buy | Strong = StrongBuy (= 5)`. |
| 20 | **?. ?? ??= operators** | 0.5 | `Services/StockSenseOptions.cs` lines 23–24; `Models/StockPrice.cs` line 41; `Console/WatchlistManager.cs` line 69 | `BaseUrl ??= "..."`, `ApiKey ?? throw`, `left?.Equals(right) ?? right is null`, `ReadLine()?.Trim()` | `??=` assigns only if null. `??` provides a fallback. `?.` calls a method only if the object is not null. All three demonstrated across these files. |
| 21 | **Pattern matching** | 1.0 | `Services/SignalEngine.cs` lines 79–86; `Services/CachedStockDataProvider.cs` lines 32, 40; `Indicators/RsiIndicator.cs` lines 69–74 | Tuple, property, and relational patterns | Tuple switch `(buyCount, sellCount) switch { var (b,s) when b>=2 => ... }`, property pattern `cached is List<StockPrice> { Count: > 0 }`, relational pattern `< 30 => Buy, > 70 => Sell`. |

**Total: 14 / 14 points**

---

## Known limitations

- **Free API tier**: 25 requests/day and 5/minute. Analyzing a 5-symbol watchlist uses 5 daily requests. Plan accordingly.
- **Golden cross fires rarely**: it only triggers on the day SMA-20 crosses SMA-50. Most analyses will show `None` for the MA signal — this is correct and expected.
- **Console.Clear()** does not fully clear VS Code's integrated terminal (scrollback remains). The app looks correct in Windows Terminal or cmd.exe.
- **~4 months of history**: the `compact` output size from Alpha Vantage returns the last 100 daily bars.
