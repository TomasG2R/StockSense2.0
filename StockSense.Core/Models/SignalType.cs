using System;

namespace StockSense.Core.Models;

[Flags]
public enum SignalType
{
    // TODO: Define how signals are combined/interpreted across indicators and alerts.
    None = 0,
    Buy = 1 << 0,
    Sell = 1 << 1,
    Strong = 1 << 2,
    Weak = 1 << 3,
}

