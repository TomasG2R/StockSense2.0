namespace StockSense.Core.Models;

/// <summary>
/// Bitwise flags describing the type of trading signal detected.
/// Combine with | to represent compound signals.
/// </summary>
/// <remarks>
/// Examples:
///   StrongBuy  = Buy  | Strong  (= 5)
///   StrongSell = Sell | Strong  (= 6)
///   WeakBuy    = Buy  | Weak    (= 9)
/// </remarks>
[Flags]
public enum SignalType
{
    None   = 0,
    Buy    = 1 << 0,   // 1
    Sell   = 1 << 1,   // 2
    Strong = 1 << 2,   // 4
    Weak   = 1 << 3,   // 8
}
