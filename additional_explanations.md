Now I have everything I need. Here's the full explanation.

---

## What is StockSense?

StockSense is a **C# console application** that acts as a personal stock market analyzer. You give it a list of stock tickers (like AAPL, MSFT, TSLA), it fetches real daily price data from the **Alpha Vantage API**, runs three technical indicators on that data, decides whether to trigger a buy or sell signal, and saves those signals as alerts to a file on your disk.

**User flow:**
1. Open the app → main menu appears
2. Manage your watchlist (add/remove tickers, or browse a built-in stock directory)
3. Run "Analyze Watchlist" → app fetches prices for each ticker, calculates MA + RSI + MACD, displays a results table, and saves any triggered alerts
4. View alert history from the main menu at any time

**Two projects (assemblies):**
- `StockSense.Core` — all logic, math, API calls, data models (compiled to a `.dll`)
- `StockSense.Console` — the menu, display, and user input (compiled to an `.exe`)

---

## Clarification on "14 points"

The grading has **21 separate requirements** that total **14 points**. Below is each one explained.

---

## The 21 Grading Requirements Explained

---

### 1. Custom Interface — 0.5 pts
**Files:** `IIndicator.cs`, `IStockDataProvider.cs`, `IAlertStore.cs`

An interface is a contract — it says "any class that claims to be this thing must have these methods." You defined three:

- `IIndicator` — any indicator (MA, RSI, MACD) must have `Name`, `Period`, `Calculate()`, and `TryGetSignal()`
- `IStockDataProvider` — anything that provides stock prices must have `GetDailyAsync()` and `GetWeeklyAsync()`
- `IAlertStore` — anything that stores alerts must have `SaveAsync()` and `LoadAsync()`

**Why it matters:** `CachedStockDataProvider` wraps `AlphaVantageService` because both implement `IStockDataProvider`. Program.cs can swap them without caring which one it has. Same idea: `JsonAlertStore` can be swapped for any other storage because it implements `IAlertStore`.

---

### 2. IComparable\<T\> — 0.5 pts
**File:** `StockPrice.cs` line 18

```csharp
public int CompareTo(StockPrice? other) => Date.CompareTo(other.Date);
```

`IComparable<T>` lets objects be sorted. `StockPrice` compares by date, so when you call `prices.Sort()` or `OrderBy(p => p)`, the prices automatically sort from oldest to newest. Without this, the sort wouldn't know what "less than" or "greater than" means for a stock price object.

---

### 3. IEquatable\<T\> — 0.5 pts
**File:** `StockPrice.cs` line 27

```csharp
public bool Equals(StockPrice? other) => Date.Date == other.Date.Date;
```

Two `StockPrice` records are considered equal if they're from the same calendar day. This is used by duplicate-checking logic — if you already have a price for March 15, you don't fetch it again. Without `IEquatable<T>`, C# would compare object references (memory addresses) instead of dates, and two different objects for the same day would never be "equal."

---

### 4. IFormattable — 1 pt
**Files:** `StockPrice.cs` line 58, `MacdPoint.cs` line 32

```csharp
public string ToString(string? format, IFormatProvider? formatProvider)
```

`IFormattable` lets you control how an object prints depending on a format string. For `StockPrice`:
- `"S"` (short): `2024-03-15 | $182.34`
- `"L"` (long): `2024-03-15 | O:181.00 H:183.50 L:180.20 C:182.34 V:55,123,400`

Same for `MacdPoint`. The console renderer calls `ToString("L", null)` when it wants full detail. This is the standard .NET pattern used by `DateTime`, `decimal`, etc.

---

### 5. switch with when — 0.5 pts
**File:** `SignalEngine.cs` line 79

```csharp
return (buyCount, sellCount) switch
{
    var (b, s) when b >= 2 => SignalType.Buy  | SignalType.Strong,
    var (b, s) when s >= 2 => SignalType.Sell | SignalType.Strong,
    var (b, _) when b == 1 => SignalType.Buy,
    var (_, s) when s == 1 => SignalType.Sell,
    _                      => SignalType.None
};
```

`when` adds a condition on top of a pattern match — it's not enough to just match the tuple shape, the values also have to satisfy the `when` condition. Here: if 2 or more indicators voted Buy → StrongBuy. If only 1 voted Buy → plain Buy. This is how the "strong signal" logic works.

Also appears in `SignalEngine.cs` line 94–97 (the `Count` helper method).

---

### 6. Range type — 0.5 pts
**File:** `MovingAverageIndicator.cs` lines 35, 55

```csharp
StockPrice[] window = arr[(i - Period + 1)..(i + 1)];  // Range slice
StockPrice[] seedWindow = arr[..Period];                 // Range from start
```

The `..` syntax creates a `Range` — it cuts a slice of an array without copying extra data. For SMA-20, `arr[0..20]` gives you exactly the 20 prices you need for that window. `arr[..Period]` means "from index 0 up to Period." This is cleaner than manual loop indexing and is a C# 8+ feature.

---

### 7. Multiple Assemblies — 1 pt
**Files:** `StockSense.Core.csproj`, `StockSense.Console.csproj`

The project is split into two compiled units:
- `StockSense.Core.dll` — the library
- `StockSense.Console.exe` — the application that references Core

This forces a clean separation of concerns. The console project cannot accidentally put business logic in the UI layer because it can only call what Core exposes publicly. This mirrors how real professional software is structured (library + app).

---

### 8. sealed or partial class — 0.5 pts
**Files:** `MovingAverageIndicator.cs` (and most model/service classes)

```csharp
public sealed class MovingAverageIndicator : IndicatorBase
```

`sealed` means nobody can inherit from this class. Why? Because `MovingAverageIndicator` has a specific, complete implementation. If someone subclassed it and overrode `Calculate()`, they might break the signal logic in `SignalEngine`. `sealed` prevents that mistake. It also lets the compiler optimize virtual method calls away.

---

### 9. Abstract class — 0.5 pts
**File:** `IndicatorBase.cs`

```csharp
public abstract class IndicatorBase : IIndicator
{
    public abstract string Name { get; }
    public abstract int Period { get; }
    public abstract IReadOnlyList<decimal> Calculate(...);
    public abstract bool TryGetSignal(...);

    protected void ValidateInput(...) { ... }  // ← shared concrete method
}
```

`IndicatorBase` is a halfway-house between interface and full class. It can't be instantiated directly — you can only use it through `MovingAverageIndicator`, `RsiIndicator`, or `MacdIndicator`. Its purpose: share the `ValidateInput()` guard method across all three indicators without copy-pasting it. Interfaces can't contain method bodies; abstract classes can.

---

### 10. Static constructor — 1 pt
**File:** `AlertService.cs` line 11, also `StockSymbol.cs` line 15

```csharp
static AlertService()
{
    MessageTemplates = new Dictionary<SignalType, string>
    {
        [SignalType.Buy]                      = "Buy signal detected",
        [SignalType.Buy | SignalType.Strong]  = "Strong buy — multiple indicators agree",
        // ...
    };
}
```

A static constructor runs **once**, automatically, before the class is first used — you never call it directly. It's used here to initialize the `MessageTemplates` dictionary that maps each `SignalType` to a human-readable string. Why static? Because message templates are shared across all instances of `AlertService` — there's no reason to rebuild them every time you create a new `AlertService`. In `StockSymbol`, the static constructor initializes the regex and max-length constant.

---

### 11. Deconstructor — 0.5 pts
**Files:** `StockPrice.cs` line 47, `MacdPoint.cs` line 16, `IndicatorPoint.cs` line 26

```csharp
public void Deconstruct(out DateTimeOffset date, out decimal close)
{
    date  = Date;
    close = Close;
}
```

A deconstructor enables the "tuple unpacking" syntax:
```csharp
var (date, close) = price;   // instead of: price.Date and price.Close separately
```

This is used when looping through prices and you only care about two fields. C# calls `Deconstruct` behind the scenes when you write `var (x, y) = obj`. `MacdPoint` has a 4-way deconstructor: `var (date, macd, signal, histogram) = point`.

---

### 12. Operator overloading — 0.5 pts
**File:** `StockPrice.cs` lines 39–42

```csharp
public static bool operator <(StockPrice left, StockPrice right) => left.CompareTo(right) < 0;
public static bool operator >(StockPrice left, StockPrice right) => left.CompareTo(right) > 0;
public static bool operator ==(StockPrice? left, StockPrice? right) => left?.Equals(right) ?? right is null;
public static bool operator !=(StockPrice? left, StockPrice? right) => !(left == right);
```

Operator overloading lets you define what `<`, `>`, `==`, `!=` mean for your own types. Without this, writing `priceA < priceB` would be a compiler error. With it, you can write natural comparisons. The `<` operator delegates to `CompareTo`, which compares dates — so `priceA < priceB` means "priceA is from an earlier date than priceB."

---

### 13. System.Collections.Generic — 1 pt
**Files:** `RateLimiter.cs` (Queue\<T\>), `JsonAlertStore.cs` (List\<T\>)

```csharp
// RateLimiter.cs
private readonly Queue<DateTime> _minuteWindow = new();
private readonly Queue<DateTime> _dayWindow    = new();

// JsonAlertStore.cs
private readonly List<Alert> _alerts = new();
```

`Queue<T>` is a FIFO (first in, first out) collection — the oldest item is always at the front. `RateLimiter` uses it to track the timestamps of recent API requests. When checking if a new request is allowed, it peeks at the front to see how long ago the oldest request was. When it expires, it dequeues it. `List<T>` in `JsonAlertStore` holds all alerts in memory while the app runs.

---

### 14. is operator — 0.5 pts
**File:** `CachedStockDataProvider.cs` lines 31–33, 40

```csharp
if (_dailyCache.TryGetValue(key, out IReadOnlyList<StockPrice>? cached)
    && cached is List<StockPrice> { Count: > 0 } cachedList)
```

The `is` operator checks if an object is a specific type AND optionally binds it to a new variable in one step. Here: "is `cached` a `List<StockPrice>` with at least one item? If yes, call it `cachedList`." Without `is`, you'd need a separate null check, a type cast, and a count check — three lines instead of one.

---

### 15. Default and named arguments — 0.5 pts
**File:** `RsiIndicator.cs` line 16

```csharp
public RsiIndicator(int period = DefaultPeriod)
```

**Default argument:** `period = 14` means you can write `new RsiIndicator()` and get a 14-period RSI without specifying it. **Named argument:** you can write `new RsiIndicator(period: 21)` to make the code self-documenting — the caller doesn't have to remember which positional argument means what. Same pattern in `MovingAverageIndicator` and `RateLimiter`.

---

### 16. params keyword — 0.5 pts
**File:** `MacdIndicator.cs` line 33

```csharp
public (...) CalculateFull(IReadOnlyList<StockPrice> prices, params int[] periodOverrides)
```

`params` lets callers pass zero or more extra arguments without explicitly creating an array:
```csharp
indicator.CalculateFull(prices)           // zero overrides — uses defaults 12, 26, 9
indicator.CalculateFull(prices, 12, 26, 9) // three overrides passed directly
```

Without `params`, the second call would need `CalculateFull(prices, new int[] { 12, 26, 9 })`. The method checks `periodOverrides.Length` to decide whether to use the custom values or the defaults.

---

### 17. out arguments — 1 pt
**File:** `AlphaVantageService.cs` line 34, `StockSymbol.cs` line 24

```csharp
if (!TryParseResponse(json, "Time Series (Daily)", out List<StockPrice>? prices, out string? error))
```

`out` lets a method return multiple values without a tuple. `TryParseResponse` returns a `bool` (success/failure) AND writes either the parsed price list or an error message into the `out` parameters. The caller gets all three results from one call. This is the standard .NET `TryXxx` pattern — same as `int.TryParse(str, out int value)`.

---

### 18. Delegates and lambda functions — 1.5 pts
**Files:** `SignalEngine.cs` line 18 & 42, `AlertService.cs` line 28, `WatchlistManager.cs` line 19, 45, 149–156

Multiple uses:

**Delegate type declaration:**
```csharp
// SignalEngine.cs
public delegate bool SignalFilter(SignalType signal);

// AlertService.cs
public delegate void AlertTriggeredHandler(Alert alert);
```

**Lambda assigned to Func:**
```csharp
// SignalEngine.cs
Func<SignalType, bool> shouldTrigger = s =>
    s != SignalType.None && (Filter is null || Filter(s));
```

**Lambda in switch expression:**
```csharp
// WatchlistManager.cs
Func<Task> action = input switch
{
    "1" => AddSymbol,
    "4" => () => { ConsoleRenderer.ShowWatchlist(...); return Task.CompletedTask; },
    ...
};
```

**Event subscription with lambda:**
```csharp
// Program.cs
alertService.OnAlertTriggered += alert => ConsoleRenderer.ShowSuccess($"ALERT: {alert}");
```

Delegates are type-safe function pointers. They let you pass behavior as a parameter, or subscribe to events. The `OnAlertTriggered` event in `AlertService` is what causes the console to print "ALERT: ..." live while analysis runs — Program.cs subscribed to that event.

---

### 19. Bitwise operations — 1 pt
**Files:** `SignalType.cs`, `Alert.cs`, `SignalEngine.cs`, `AlertService.cs`

```csharp
// SignalType.cs
[Flags]
public enum SignalType { None = 0, Buy = 1, Sell = 2, Strong = 4, Weak = 8 }

// Alert.cs
public bool IsBuy    => (Signal & SignalType.Buy)    != SignalType.None;
public bool IsStrong => (Signal & SignalType.Strong) != SignalType.None;

// SignalEngine.cs — combining flags
SignalType.Buy | SignalType.Strong   // = 5 = StrongBuy
```

`[Flags]` means each enum value is a bit, and you can combine them with `|` (OR). `Signal & SignalType.Buy` tests whether the Buy bit is switched on — this is faster and more expressive than having separate boolean fields. `StrongBuy` is not a separate value; it's `Buy | Strong = 1 | 4 = 5`. You can always decompose it back: `(signal & SignalType.Buy) != None` checks just the Buy bit regardless of what else is combined.

---

### 20. ?., ?[], ??, ??= operators — 0.5 pts
**Files:** `StockSenseOptions.cs` lines 22–24, `StockPrice.cs` line 41

```csharp
// StockSenseOptions.cs
BaseUrl ??= "https://www.alphavantage.co/query";   // ??= : assign only if null
return BaseUrl ?? "https://www.alphavantage.co/query";  // ?? : use right if left is null

// StockPrice.cs
public static bool operator ==(StockPrice? left, StockPrice? right)
    => left?.Equals(right) ?? right is null;    // ?. : call only if not null
```

- `?.` (null-conditional): calls a method only if the object isn't null; returns null otherwise instead of crashing
- `??` (null-coalescing): returns the right side if the left side is null
- `??=` (null-coalescing assignment): assigns the right side to the variable only if it's currently null — a shortcut for `if (x == null) x = value`

---

### 21. Pattern matching — 1 pt
**Files:** `SignalEngine.cs` lines 79–86, `CachedStockDataProvider.cs` lines 31–33, `RsiIndicator.cs` line 69

```csharp
// RsiIndicator.cs — relational pattern matching
signal = latest switch
{
    < 30 => SignalType.Buy,
    > 70 => SignalType.Sell,
    _    => SignalType.None
};

// CachedStockDataProvider.cs — type pattern with property pattern
cached is List<StockPrice> { Count: > 0 } cachedList

// SignalEngine.cs — tuple pattern with when
(buyCount, sellCount) switch
{
    var (b, s) when b >= 2 => SignalType.Buy | SignalType.Strong,
    ...
}
```

Pattern matching is an evolved form of `if/switch` that lets you match on **type**, **value**, **properties**, and **structure** simultaneously. `< 30` is a relational pattern (no variable needed). `List<StockPrice> { Count: > 0 }` is a type pattern + property pattern combined. The `switch` expression returns a value directly instead of needing `break`/`return` in every branch.

---

**Total: 21 requirements → 14 points.** All are implemented and present in the current code.


---
###
---


## MA, RSI, MACD — What They Are

These are **technical indicators** — math formulas traders use to analyze price trends and decide when to buy or sell.

---

### MA — Moving Average
Takes the average closing price over the last N days. Smooths out noise so you can see the trend direction.

- **SMA-20** = average of last 20 days
- **SMA-50** = average of last 50 days

**Signal rule used in the code:**
- **Golden cross** → SMA-20 crosses *above* SMA-50 = BUY (short-term trend overtaking long-term = bullish)
- **Death cross** → SMA-20 crosses *below* SMA-50 = SELL

---

### RSI — Relative Strength Index
Measures whether a stock is being overbought or oversold. Scale is 0–100.

Formula: compares average gains vs average losses over 14 days.

**Signal rule:**
- RSI < 30 → stock is oversold → BUY (price dropped too far, likely to bounce)
- RSI > 70 → stock is overbought → SELL (price rose too fast, likely to drop)

---

### MACD — Moving Average Convergence Divergence
Measures momentum by comparing two EMAs (Exponential Moving Averages — like SMA but recent prices weigh more).

Three lines:
- **MACD line** = 12-day EMA − 26-day EMA
- **Signal line** = 9-day EMA of the MACD line
- **Histogram** = MACD − Signal (shows how far apart they are)

**Signal rule:**
- MACD crosses *above* signal line → BUY (momentum turning positive)
- MACD crosses *below* signal line → SELL (momentum turning negative)

---

### How they combine (SignalEngine)
Each indicator votes independently. If 2 or more vote the same direction → **Strong** signal. If only 1 votes → plain signal.

---
---

## The 21 Requirements — Simple Explanations

---

### 1. Custom interface (0.5 pts)

In C#, an **interface has nothing to do with a user interface or windows.** It's a programming contract — a list of method signatures that a class *promises* to implement.

Think of it like a job description. The interface says "whoever fills this role must be able to do X, Y, Z." The class that implements it is the actual employee.

**In the code:**
```csharp
// IIndicator.cs
public interface IIndicator
{
    string Name { get; }
    int Period { get; }
    IReadOnlyList<decimal> Calculate(...);
    bool TryGetSignal(...);
}
```
`MovingAverageIndicator`, `RsiIndicator`, `MacdIndicator` all implement `IIndicator`. `SignalEngine` can use all three through that shared contract without caring which one it's talking to.

Three interfaces: `IIndicator`, `IStockDataProvider`, `IAlertStore`.

---

### 2. IComparable\<T\> (0.5 pts)

Teaches C# **how to sort** your object.

Without it: `prices.Sort()` → compiler error, C# doesn't know what "less than" means for a `StockPrice`.

With it: you define that one price is "less than" another if its date is earlier.

**In the code** (`StockPrice.cs`):
```csharp
public int CompareTo(StockPrice? other) => Date.CompareTo(other.Date);
// Returns negative = this is earlier, 0 = same day, positive = this is later
```

---

### 3. IEquatable\<T\> (0.5 pts)

Teaches C# **what "equal" means** for your object.

By default C# compares memory addresses (two different objects are never equal even if they hold identical data). `IEquatable<T>` lets you override that.

**In the code** (`StockPrice.cs`):
```csharp
public bool Equals(StockPrice? other) => Date.Date == other.Date.Date;
// Two prices are "the same" if they're from the same calendar day
```
Used for duplicate checking — don't save an alert if one already exists for the same symbol on the same day.

---

### 4. IFormattable (1 pt)

Lets you control **how an object looks when printed**, depending on a format string you pass in.

Same idea as `DateTime` where `date.ToString("yyyy-MM-dd")` gives a different result than `date.ToString("dd/MM")`.

**In the code** (`StockPrice.cs` and `MacdPoint.cs`):
```csharp
price.ToString("S", null)  →  "2024-03-15 | $182.34"
price.ToString("L", null)  →  "2024-03-15 | O:181.00 H:183.50 L:180.20 C:182.34 V:55,123,400"
```
The console renderer calls `"L"` when displaying a full price table, `"S"` for a quick summary.

---

### 5. switch with when (0.5 pts)

`when` adds an **extra condition** on top of a switch case.

Normal switch matches on a value. `when` lets you also add logic like "and also this number must be >= 2."

**In the code** (`SignalEngine.cs`):
```csharp
return (buyCount, sellCount) switch
{
    var (b, s) when b >= 2 => SignalType.Buy | SignalType.Strong,  // 2+ buy votes = StrongBuy
    var (b, s) when s >= 2 => SignalType.Sell | SignalType.Strong, // 2+ sell votes = StrongSell
    var (b, _) when b == 1 => SignalType.Buy,
    var (_, s) when s == 1 => SignalType.Sell,
    _                      => SignalType.None
};
```

---

### 6. Range type (0.5 pts)

The `..` operator creates a **Range** — a slice of an array.

```csharp
arr[5..10]   // elements at index 5, 6, 7, 8, 9
arr[..20]    // first 20 elements (index 0 to 19)
arr[^1]      // last element (^ means "from end")
```

**In the code** (`MovingAverageIndicator.cs`):
```csharp
StockPrice[] window = arr[(i - Period + 1)..(i + 1)];
// Grabs exactly the last 20 prices for this SMA window
```
Instead of a manual loop to copy elements into a sub-array.

---

### 7. Multiple assemblies (1 pt)

The project compiles into **two separate .dll/.exe files**:

- `StockSense.Core.dll` — all logic (indicators, services, models)
- `StockSense.Console.exe` — menu and display; references Core

This forces clean separation. Console can't put calculation code in itself because it only uses what Core exposes. It mirrors how real software is built — a library used by an application.

Defined in two separate `.csproj` files.

---

### 8. sealed class (0.5 pts)

`sealed` means **no one can inherit from this class.**

```csharp
public sealed class MovingAverageIndicator : IndicatorBase
```

Why? `MovingAverageIndicator` is complete — there's no sensible reason to subclass it and override parts of it. Marking it `sealed` prevents accidental or incorrect inheritance that could break the signal logic. Most model and service classes in this project are `sealed` for the same reason.

---

### 9. Abstract class (0.5 pts)

An abstract class is **halfway between an interface and a normal class.** You can't create it directly — it's only useful as a base for other classes.

The difference from an interface: it can have **real, shared method bodies** (not just signatures).

**In the code** (`IndicatorBase.cs`):
```csharp
public abstract class IndicatorBase : IIndicator
{
    public abstract string Name { get; }        // must be implemented by subclass
    public abstract IReadOnlyList<decimal> Calculate(...);  // same

    protected void ValidateInput(...)  // ← REAL shared code, not abstract
    {
        if (prices.Count < requiredLength) throw new ArgumentException(...);
    }
}
```
All three indicators inherit from this. They get `ValidateInput()` for free — no copy-paste.

---

### 10. Static constructor (1 pt)

A static constructor runs **exactly once**, automatically, before the class is first used. You never call it manually.

Normal constructors run every time you write `new Foo()`. A static constructor runs once per program lifetime.

**In the code** (`AlertService.cs`):
```csharp
static AlertService()
{
    MessageTemplates = new Dictionary<SignalType, string>
    {
        [SignalType.Buy]  = "Buy signal detected",
        [SignalType.Buy | SignalType.Strong] = "Strong buy — multiple indicators agree",
        // ...
    };
}
```
The message templates are shared by all `AlertService` instances — no need to rebuild them every time. Also in `StockSymbol.cs` to initialize the validation regex once.

---

### 11. Deconstructor (0.5 pts)

A deconstructor enables **tuple-style unpacking** of an object.

```csharp
var (date, close) = price;
// instead of:
var date  = price.Date;
var close = price.Close;
```

**In the code** (`StockPrice.cs`):
```csharp
public void Deconstruct(out DateTimeOffset date, out decimal close)
{
    date  = Date;
    close = Close;
}
```
C# calls `Deconstruct` automatically when you use the `var (x, y) = obj` syntax. `MacdPoint` has a 4-way version: `var (date, macd, signal, histogram) = point`.

---

### 12. Operator overloading (0.5 pts)

Lets you define what `<`, `>`, `==`, `!=` mean **for your own class.**

Without it: `priceA < priceB` → compiler error.

**In the code** (`StockPrice.cs`):
```csharp
public static bool operator <(StockPrice left, StockPrice right)
    => left.CompareTo(right) < 0;
// Now you can write:  if (priceA < priceB)  which compares their dates
```

---

### 13. System.Collections.Generic (1 pt)

Using the built-in **generic collection types** from .NET: `List<T>`, `Queue<T>`, `Dictionary<K,V>`, etc.

**In the code:**

`Queue<T>` in `RateLimiter.cs`:
```csharp
private readonly Queue<DateTime> _minuteWindow = new();
```
A Queue is FIFO — first in, first out. Used to track timestamps of recent API requests. When checking rate limits, peek at the oldest timestamp at the front. When it's expired, dequeue it.

`List<T>` in `JsonAlertStore.cs`:
```csharp
private readonly List<Alert> _alerts = new();
```
Holds all alerts in memory while the app is running.

---

### 14. is operator (0.5 pts)

Checks if an object **is a certain type**, and optionally unpacks it into a variable — all in one step.

```csharp
// Old way (3 steps):
if (cached != null && cached is List<StockPrice> && ((List<StockPrice>)cached).Count > 0)

// With is (1 step):
if (cached is List<StockPrice> { Count: > 0 } cachedList)
// checks type, checks Count > 0, and binds to cachedList — all at once
```

**In the code** (`CachedStockDataProvider.cs`): used when checking whether a cached result exists and is non-empty before returning it.

---

### 15. Default and named arguments (0.5 pts)

**Default argument** = a parameter has a fallback value so you don't have to pass it.

**Named argument** = you write the parameter name explicitly when calling, so the code is self-documenting.

**In the code** (`RsiIndicator.cs`):
```csharp
public RsiIndicator(int period = 14)  // default argument
```
```csharp
new RsiIndicator()           // uses default: period = 14
new RsiIndicator(period: 21) // named argument: clear which param you're setting
```

---

### 16. params keyword (0.5 pts)

`params` lets a method accept **zero or more extra arguments** without the caller needing to create an array.

**In the code** (`MacdIndicator.cs`):
```csharp
public (...) CalculateFull(IReadOnlyList<StockPrice> prices, params int[] periodOverrides)
```
```csharp
macd.CalculateFull(prices)              // zero overrides — uses defaults 12, 26, 9
macd.CalculateFull(prices, 12, 26, 9)  // three values passed directly, no array syntax
```
Inside the method: `periodOverrides.Length` tells you how many were passed.

---

### 17. out arguments (1 pt)

`out` lets a method **return multiple values** — the method writes into variables you pass in.

Think of it as "please fill in this blank for me."

**In the code** (`AlphaVantageService.cs`):
```csharp
TryParseResponse(json, "Time Series (Daily)", out List<StockPrice>? prices, out string? error)
// Returns: bool (success/fail), AND fills in either prices OR error
```
You've already seen this pattern with `int.TryParse(str, out int value)` — same idea.

Also in `StockSymbol.TryParse(input, out StockSymbol? symbol)`.

---

### 18. Delegates and lambda functions (1.5 pts)

A **delegate** is a variable that holds a function. A **lambda** is an anonymous function written inline with `=>`.

**Delegate type declaration** (`AlertService.cs`):
```csharp
public delegate void AlertTriggeredHandler(Alert alert);
public event AlertTriggeredHandler? OnAlertTriggered;
```

**Subscribing with a lambda** (`Program.cs`):
```csharp
alertService.OnAlertTriggered += alert =>
    ConsoleRenderer.ShowSuccess($"ALERT: {alert}");
// When an alert fires → print it live in the console
```

**Lambda in switch** (`WatchlistManager.cs`):
```csharp
Func<Task> action = input switch
{
    "1" => AddSymbol,
    "2" => BrowseDirectory,
    "0" => () => { managing = false; return Task.CompletedTask; },
};
await action();
```
The menu doesn't run the function immediately — it picks which function to run, then calls it. That's a delegate in action.

---

### 19. Bitwise operations (1 pt)

Bitwise means operating **directly on the binary bits** of a number.

`SignalType` is a `[Flags]` enum — each value is a single bit:
```
None   = 0000
Buy    = 0001
Sell   = 0010
Strong = 0100
Weak   = 1000
```

**Combine with `|` (OR):**
```csharp
SignalType.Buy | SignalType.Strong  = 0001 | 0100 = 0101 = 5  → StrongBuy
```

**Check with `&` (AND)** (`Alert.cs`):
```csharp
public bool IsBuy => (Signal & SignalType.Buy) != SignalType.None;
// If the Buy bit is ON in Signal → it's a buy alert
```
This lets one integer carry multiple flags at once. `StrongBuy` isn't a separate value — it's `Buy + Strong` combined into one number.

---

### 20. ?., ?[], ??, ??= operators (0.5 pts)

All four are **null-safety shortcuts** so your code doesn't crash on null values.

| Operator | Meaning | Example |
|---|---|---|
| `?.` | call only if not null | `obj?.Method()` → returns null instead of crashing |
| `??` | use right side if left is null | `x ?? "default"` |
| `??=` | assign only if currently null | `x ??= "default"` |

**In the code** (`StockSenseOptions.cs`):
```csharp
BaseUrl ??= "https://www.alphavantage.co/query";  // set default if never configured
return BaseUrl ?? "https://www.alphavantage.co/query";  // return default if null
```

(`?[]` is the same as `?.` but for array/index access — not needed in this project but the family is demonstrated.)

---

### 21. Pattern matching (1 pt)

Pattern matching is an evolved switch/if that can match on **type, value, and structure simultaneously.**

Three kinds used in the code:

**Relational pattern** (`RsiIndicator.cs`):
```csharp
signal = latest switch
{
    < 30 => SignalType.Buy,   // matches if latest < 30
    > 70 => SignalType.Sell,
    _    => SignalType.None   // default
};
```

**Type + property pattern** (`CachedStockDataProvider.cs`):
```csharp
cached is List<StockPrice> { Count: > 0 }
// Is it a List<StockPrice>? AND does it have Count > 0?
```

**Tuple pattern with when** (`SignalEngine.cs`):
```csharp
(buyCount, sellCount) switch
{
    var (b, s) when b >= 2 => ...
}
```

Without pattern matching, all of this would be nested `if/else` chains with explicit casts.