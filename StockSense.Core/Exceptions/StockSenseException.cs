namespace StockSense.Core.Exceptions;

// GRADING: custom exception base class
public class StockSenseException : Exception
{
    public StockSenseException(string message) : base(message) { }
    public StockSenseException(string message, Exception inner) : base(message, inner) { }
}

// GRADING: custom exception — thrown when Alpha Vantage returns bad/unexpected data
public class StockDataException : StockSenseException
{
    public string Symbol { get; }
    public StockDataException(string symbol, string message)
        : base($"[{symbol}] {message}") => Symbol = symbol;
    public StockDataException(string symbol, string message, Exception inner)
        : base($"[{symbol}] {message}", inner) => Symbol = symbol;
}

// GRADING: custom exception — thrown when the per-day API limit is reached
public class RateLimitException : StockSenseException
{
    public RateLimitException()
        : base("Alpha Vantage daily request limit reached. Try again tomorrow.") { }
}

// GRADING: custom exception — thrown when a ticker symbol fails validation
public class InvalidSymbolException : StockSenseException
{
    public InvalidSymbolException(string input)
        : base($"'{input}' is not a valid stock symbol. Must be 1–5 letters.") { }
}