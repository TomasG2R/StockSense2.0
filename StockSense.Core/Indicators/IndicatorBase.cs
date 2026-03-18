  using StockSense.Core.Interfaces;
  using StockSense.Core.Models;

  namespace StockSense.Core.Indicators;

  /// <summary>
  /// Shared base for all technical indicators. Cannot be instantiated directly.
  /// </summary>
  public abstract class IndicatorBase : IIndicator
  {
      /// <inheritdoc/>
      public abstract string Name { get; }

      /// <inheritdoc/>
      public abstract int Period { get; }

      /// <inheritdoc/>
      public abstract IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices);

      /// <inheritdoc/>
      public abstract bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal);

      /// <summary>
      /// Throws if the price list is null or has fewer entries than the required period.
      /// Call this at the top of every Calculate/TryGetSignal implementation.
      /// </summary>
      protected void ValidateInput(IReadOnlyList<StockPrice> prices, int requiredLength)
      {
          ArgumentNullException.ThrowIfNull(prices);
          if (prices.Count < requiredLength)
              throw new ArgumentException(
                  $"{Name} needs at least {requiredLength} data points, got {prices.Count}.");
      }
  }