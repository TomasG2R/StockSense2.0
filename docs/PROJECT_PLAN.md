# StockSense — Recommended Structure & Implementation Plan

This document proposes a clean, grader-friendly structure for **StockSense** (WPF desktop app) and a concrete 6‑week implementation plan. It is designed to satisfy the mandatory university C# feature requirements in *real, demonstrable code paths*.

## Goals (what the finished app does)

- Fetch real stock price history (primary: **Alpha Vantage**; swappable provider)
- Compute indicators: **SMA/EMA**, **RSI**, **MACD**
- Detect **Buy/Sell** signals based on indicator values
- Trigger & persist **alerts** when signals fire
- Display data + indicators + alerts in a clean **WPF UI with charts**

## High-level architecture

- **`StockSense.Core`** (class library): domain models, indicator math, signal rules, alerting & persistence abstractions, API providers
- **`StockSense.WPF`** (WPF app): MVVM UI, chart rendering, user settings, binds to Core services

Keep Core **UI-free**. Keep WPF **logic-light** (no indicator math in the UI).

## Recommended solution structure (folders + key files)

### `StockSense.Core/`

Suggested folders (you already started most of these):

- `Models/`
  - `StockSymbol.cs` (sealed)
  - `StockPrice.cs`
  - `SignalType.cs` (`[Flags]` enum)
  - `IndicatorPoint.cs` (timestamp + value)
  - `MacdPoint.cs` (timestamp + MACD/signal/histogram)
  - `AlertId.cs` (optional strong ID type)
- `Interfaces/`
  - `IIndicator.cs` (indicator contract)
  - `IStockDataProvider.cs` (API abstraction)
  - `IAlertStore.cs` (alert persistence abstraction)
- `Indicators/`
  - `IndicatorBase.cs` (abstract base, shared validation/range slicing)
  - `MovingAverageIndicator.cs`
  - `RsiIndicator.cs` (sealed)
  - `MacdIndicator.cs`
- `Signals/` (recommended addition; keeps rules separate from math)
  - `SignalEngine.cs` (turns indicator values into `SignalType` flags)
  - `SignalRuleSet.cs` (configurable thresholds / rule parameters)
- `Alerts/`
  - `Alert.cs`
  - `AlertService.cs` (creates alerts, raises events, calls store)
- `Services/`
  - `AlphaVantageService.cs` (uses static `HttpClient`)
  - `RateLimiter.cs` (AlphaVantage free-tier throttling)
  - `PriceCache.cs` (in-memory cache keyed by `StockSymbol`)
- `Persistence/` (recommended addition; avoids “Services” dumping ground)
  - `JsonAlertStore.cs`
  - `JsonSettingsStore.cs` (optional)

### `StockSense.WPF/`

- `Views/`
  - `MainWindow.xaml` (shell layout)
  - `Views/*.xaml` (optional split: Prices, Indicators, Alerts)
- `ViewModels/`
  - `MainViewModel.cs`
  - `ChartViewModel.cs`
  - `AlertsViewModel.cs`
- `Commands/`
  - `RelayCommand.cs` / `AsyncRelayCommand.cs`
- `Converters/`
  - `EnumFlagsToBoolConverter.cs` (useful for toggles bound to `[Flags]`)
- `Controls/`
  - `SimpleLineChart.cs` (fallback if you avoid external chart libs)
- `Resources/`
  - `Styles.xaml`, `Theme.xaml`
- `Services/` (WPF-side only)
  - `UiDispatcher.cs` (optional helper)
  - `DialogService.cs` (optional)

## Indicator contract recommendation (to match typical university specs)

If you follow the “recommended by someone else” `IIndicator` shape:

- `string Name { get; }`
- `int Period { get; }`
- `decimal[] Calculate(StockPrice[] prices)`
- `bool TryGetSignal(StockPrice[] prices, out SignalType signal)`

That’s fine for grading. (Later you can optionally generalize to generics, but don’t sacrifice clarity.)

## Mandatory university requirements — where to implement each

You must be able to *point to real code* (not dead/demo-only code). Suggested mapping:

- **IComparable<T>**
  - `StockPrice : IComparable<StockPrice>` (compare by timestamp)
  - or `Alert : IComparable<Alert>` (compare by created time)
- **IEquatable<T>**
  - `StockSymbol : IEquatable<StockSymbol>`
- **IFormattable**
  - `StockSymbol : IFormattable` (formats symbol, exchange, etc.)
- **Own interface**
  - `IIndicator`, `IStockDataProvider`, `IAlertStore`
- **Abstract class**
  - `IndicatorBase : IIndicator`
- **Sealed class**
  - `StockSymbol` and/or `RsiIndicator`
- **Partial class**
  - WPF already uses partial classes: `MainWindow`, `App` (generated + code-behind)
- **Static constructor**
  - `IndicatorBase` static ctor (e.g., precomputed constants)
  - and/or `AlphaVantageService` static ctor (initialize `HttpClient`)
- **Deconstructor**
  - `StockPrice.Deconstruct(out DateTimeOffset time, out decimal close)`
- **Operator overloading**
  - `StockSymbol ==` and `!=`
- **Range type**
  - `IndicatorBase.Calculate(StockPrice[] prices, Range? range = null)` (optional) or internal helpers that accept `Range`
- **switch with when**
  - `SignalEngine` rule evaluation (e.g., RSI thresholds, MACD crossovers)
- **is operator**
  - WPF side: handling indicator implementations or result types
- **params keyword**
  - `MovingAverageIndicator(params int[] periods)`
- **out arguments**
  - `StockSymbol.TryParse(string, out StockSymbol symbol)`
  - `TryGetSignal(..., out SignalType signal)`
- **Default & named arguments**
  - `GetDailyAsync(symbol, start: ..., end: ..., ct: default)`
- **Delegates & lambdas**
  - `AlertService.AlertTriggered` event (delegate)
  - MVVM commands and LINQ/lambdas in ViewModels
- **Bitwise operations with `[Flags]` enum**
  - `SignalType` combinations in `SignalEngine` (`|`, `&`, checks)
- **Null-coalescing operators (`?. ?? ??=`)**
  - Options/config defaults and safe UI binding
- **Pattern matching**
  - input validation, rule evaluation, result interpretation
- **Generics (`List<T>`, `Dictionary<T,T>`)**
  - caching, indicator routing, collections returned from services
- **Multiple assemblies**
  - already satisfied (`StockSense.Core` + `StockSense.WPF`)

## 6-week step-by-step implementation plan (specific classes + methods)

### Week 1 — Domain model + Alpha Vantage fetch (end-to-end data in Core)

**Implement in `StockSense.Core`:**

- `Models/StockSymbol.cs`
  - `TryParse(string? text, out StockSymbol symbol)`
  - `Equals/GetHashCode`, `IComparable`, `IFormattable`
  - `operator ==`, `operator !=`
  - `static StockSymbol()` (static constructor)
- `Models/StockPrice.cs`
  - properties: `Time`, `Open`, `High`, `Low`, `Close`, `Volume`
  - `Deconstruct(out DateTimeOffset time, out decimal close)`
  - `IComparable<StockPrice>`
- `Interfaces/IStockDataProvider.cs`
  - `Task<IReadOnlyList<StockPrice>> GetDailyAsync(StockSymbol symbol, DateOnly? start = null, DateOnly? end = null, CancellationToken ct = default)`
- `Services/AlphaVantageService.cs`
  - `static readonly HttpClient Http = new();` (static field)
  - `static AlphaVantageService()` (configure `HttpClient`)
  - `GetDailyAsync(...)`
  - internal helpers: `BuildUri(...)`, `TryParseResponse(..., out List<StockPrice> prices)`

**University requirements satisfied this week:**

- sealed class, IComparable<T>, IEquatable<T>, IFormattable, own interface, static constructor, deconstructor, operator overloading, out args, default/named args, null-coalescing operators, multiple assemblies.

**Risks / watch-outs:**

- API rate limits; make sure you do not re-fetch on every UI interaction.
- Store API key outside git (user secrets / environment / local settings).

---

### Week 2 — Indicator framework + Moving Averages (SMA/EMA)

**Implement in `StockSense.Core`:**

- `Interfaces/IIndicator.cs` (recommended grading-friendly shape)
  - `string Name { get; }`
  - `int Period { get; }`
  - `decimal[] Calculate(StockPrice[] prices)`
  - `bool TryGetSignal(StockPrice[] prices, out SignalType signal)`
- `Indicators/IndicatorBase.cs`
  - `static IndicatorBase()` (static ctor)
  - `protected void ValidateInput(StockPrice[] prices)` (and optional `Range` validation)
- `Indicators/MovingAverageIndicator.cs`
  - ctor: `MovingAverageIndicator(params int[] periods)`
  - implement SMA/EMA in `Calculate(...)`
  - `TryGetSignal(...)` (e.g., price cross MA)

**University requirements satisfied this week:**

- own interface, abstract class, static constructor, params, out args, Range (if included), pattern matching + is (input validation), generics (`List<T>`, `Dictionary<T,T>`) for internal caching/series handling.

**Risks / watch-outs:**

- window alignment (where the first valid value appears); be consistent.

---

### Week 3 — RSI + MACD

**Implement in `StockSense.Core`:**

- `Indicators/RsiIndicator.cs` (sealed)
  - `Calculate(...)`
  - `TryGetSignal(...)` (e.g., overbought/oversold thresholds)
- `Models/MacdPoint.cs` (if you want structured MACD output)
- `Indicators/MacdIndicator.cs`
  - `Calculate(...)` producing MACD series (and signal line)
  - `TryGetSignal(...)` (crossovers)

**University requirements satisfied this week:**

- sealed class, pattern matching, switch/when (optional in thresholds), generics in intermediate lists, null-coalescing for safe default thresholds.

**Risks / watch-outs:**

- correctness is easy to get wrong; validate with known sample values (even a small hardcoded dataset).

---

### Week 4 — Signal engine + alerts + persistence

**Implement in `StockSense.Core`:**

- `Signals/SignalEngine.cs`
  - `SignalType Evaluate(StockPrice[] prices, params IIndicator[] indicators)` (params)
  - uses `switch` with `when` and pattern matching for rule evaluation
  - uses `[Flags]` bitwise combos (`|`, `&`) to combine signals
- `Alerts/Alert.cs`
  - fields: `Id`, `Symbol`, `SignalType`, `CreatedAt`, `Message`
  - implement `IComparable<Alert>` or `IEquatable<Alert>` (optional but useful)
- `Interfaces/IAlertStore.cs`
  - `SaveAsync(Alert alert, CancellationToken ct = default)`
  - `LoadAsync(StockSymbol? symbol = null, CancellationToken ct = default)`
- `Persistence/JsonAlertStore.cs`
  - atomic writes + basic corruption handling
- `Alerts/AlertService.cs`
  - `public delegate void AlertTriggeredHandler(Alert alert);`
  - `public event AlertTriggeredHandler? AlertTriggered;`
  - use lambdas in WPF to subscribe

**University requirements satisfied this week:**

- delegates & lambdas, `[Flags]` + bitwise ops, switch with when, params, named/default args, null-coalescing, pattern matching.

**Risks / watch-outs:**

- file IO locking; always write atomically (temp + replace).

---

### Week 5 — WPF MVVM UI + charting + wiring everything together

**Implement in `StockSense.WPF`:**

- `ViewModels/MainViewModel.cs`
  - properties: symbol input, range, busy/error state, series collections, alerts list
  - methods: `LoadAsync()`, `RecomputeIndicators()`, `ApplySignals()`
  - commands: `LoadCommand`, `SaveAlertCommand`, `ClearAlertsCommand`
- `Commands/AsyncRelayCommand.cs` (or similar)
- `MainWindow.xaml`
  - symbol input + date range + indicator toggles + chart + alerts list
- `Controls/SimpleLineChart.cs` (fallback) **or** integrate a chart library

**University requirements satisfied this week:**

- partial class (WPF), lambdas for command handlers, is/pattern matching for result types, null-coalescing in bindings/state.

**Risks / watch-outs:**

- keep code-behind minimal; avoid putting logic in `MainWindow.xaml.cs`.
- chart library compatibility with your target framework.

---

### Week 6 — Polish, stability, and grading “requirements audit”

**Implement improvements:**

- `Services/PriceCache.cs` (Core) using `Dictionary<StockSymbol, IReadOnlyList<StockPrice>>`
- `Services/RateLimiter.cs` (Core) for Alpha Vantage throttling
- WPF settings UI for API key (store locally, not in code)
- “Requirements audit” checklist: ensure every mandatory feature is used on real code paths (and easy to show)

**Risks / watch-outs:**

- common grading issue: feature exists but is not exercised. Make it demonstrable via the normal UI flow.

## Deliverables you can show during defense/demo

- Fetch data for a symbol + date range
- Overlay SMA/EMA, RSI, MACD
- Show computed signals (flags) and why they fired (switch/when rules)
- Create persisted alerts; restart app; alerts still present
- Point to the code locations that satisfy each mandatory C# feature

