namespace StockSense.Console;

/// <summary>
/// A hardcoded directory of well-known stock tickers grouped by sector.
/// No API call needed — always available.
/// </summary>
public static class StockDirectory
{
    public record StockEntry(string Symbol, string Company, string Sector);

    public static readonly IReadOnlyList<StockEntry> All = new List<StockEntry>
    {
        // Technology
        new("AAPL",  "Apple Inc.",                  "Technology"),
        new("MSFT",  "Microsoft Corporation",        "Technology"),
        new("GOOGL", "Alphabet (Google)",            "Technology"),
        new("AMZN",  "Amazon.com Inc.",              "Technology"),
        new("NVDA",  "NVIDIA Corporation",           "Technology"),
        new("META",  "Meta Platforms (Facebook)",    "Technology"),
        new("TSLA",  "Tesla Inc.",                   "Technology"),
        new("AMD",   "Advanced Micro Devices",       "Technology"),
        new("INTC",  "Intel Corporation",            "Technology"),
        new("CRM",   "Salesforce Inc.",              "Technology"),

        // Finance
        new("JPM",   "JPMorgan Chase & Co.",         "Finance"),
        new("BAC",   "Bank of America",              "Finance"),
        new("GS",    "Goldman Sachs Group",          "Finance"),
        new("V",     "Visa Inc.",                    "Finance"),
        new("MA",    "Mastercard Inc.",              "Finance"),

        // Healthcare
        new("JNJ",   "Johnson & Johnson",            "Healthcare"),
        new("PFE",   "Pfizer Inc.",                  "Healthcare"),
        new("UNH",   "UnitedHealth Group",           "Healthcare"),
        new("ABBV",  "AbbVie Inc.",                  "Healthcare"),
        new("MRK",   "Merck & Co.",                  "Healthcare"),

        // Consumer
        new("WMT",   "Walmart Inc.",                 "Consumer"),
        new("KO",    "Coca-Cola Company",            "Consumer"),
        new("MCD",   "McDonald's Corporation",       "Consumer"),
        new("NKE",   "Nike Inc.",                    "Consumer"),
        new("SBUX",  "Starbucks Corporation",        "Consumer"),

        // Energy
        new("XOM",   "Exxon Mobil Corporation",      "Energy"),
        new("CVX",   "Chevron Corporation",          "Energy"),

        // Industrials
        new("BA",    "Boeing Company",               "Industrials"),
        new("CAT",   "Caterpillar Inc.",             "Industrials"),
        new("GE",    "GE Aerospace",                 "Industrials"),
    };

    /// <summary>Returns all entries for a given sector.</summary>
    public static IEnumerable<StockEntry> BySector(string sector) =>
        All.Where(e => e.Sector == sector);

    /// <summary>Returns the distinct list of sectors.</summary>
    public static IEnumerable<string> Sectors =>
        All.Select(e => e.Sector).Distinct();
}
