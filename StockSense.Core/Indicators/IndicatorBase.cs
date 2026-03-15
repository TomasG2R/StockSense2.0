using StockSense.Core.Interfaces;

namespace StockSense.Core.Indicators;

public abstract class IndicatorBase : IIndicator
{
    static IndicatorBase()
    {
        // TODO: Add static initialization used by all indicators (if needed).
    }

    // TODO: Add common properties (Name, Period) and shared logic.

    protected void ValidateInput()
    {
        // TODO: Validate input price history (null/length/range).
    }
}

