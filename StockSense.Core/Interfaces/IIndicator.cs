namespace StockSense.Core.Interfaces;

public interface IIndicator
{
    // TODO: Define indicator contract:
    // - Name, Period
    // - Calculate(StockPrice[] prices) -> decimal[]
    // - TryGetSignal(StockPrice[] prices, out SignalType signal) -> bool
}

