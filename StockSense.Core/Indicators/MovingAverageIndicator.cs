  using StockSense.Core.Models;

  namespace StockSense.Core.Indicators;

  /// <summary>
  /// Calculates Simple Moving Average (SMA) and Exponential Moving Average (EMA).
  /// </summary>
  public sealed class MovingAverageIndicator : IndicatorBase
  {
      private const int DefaultPeriod = 20;

      /// <inheritdoc/>
      public override string Name => $"SMA-{Period}";

      /// <inheritdoc/>
      public override int Period { get; }

      /// <summary>Creates a MovingAverageIndicator with a custom period.</summary>
      public MovingAverageIndicator(int period = DefaultPeriod)
      {
          if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
          Period = period;
      }

      /// <summary>
      /// Returns the SMA value for each position that has enough history.
      /// Uses Range slicing on the underlying array to get each price window.
      /// </summary>
      public override IReadOnlyList<decimal> Calculate(IReadOnlyList<StockPrice> prices)
      {
          ValidateInput(prices, Period);

          // Convert to array so we can use Range slicing: array[start..end]
          StockPrice[] arr = prices as StockPrice[] ?? prices.ToArray();
          var results = new List<decimal>();

          for (int i = Period - 1; i < arr.Length; i++)
          {
              // Range type requirement: slice the window of prices for this period
              StockPrice[] window = arr[(i - Period + 1)..(i + 1)];
              decimal sum = 0;
              foreach (StockPrice p in window) sum += p.Close;
              results.Add(sum / Period);
          }

          return results;
      }

      /// <summary>
      /// Calculates EMA for the given period.
      /// First EMA value is seeded with a simple average (not zero).
      /// </summary>
      public IReadOnlyList<decimal> CalculateEma(IReadOnlyList<StockPrice> prices)
      {
          ValidateInput(prices, Period);

          StockPrice[] arr = prices as StockPrice[] ?? prices.ToArray();
          var results = new List<decimal>();
          decimal multiplier = 2m / (Period + 1);

          // Range type: seed slice arr[..Period] = first Period elements
          StockPrice[] seedWindow = arr[..Period];
          decimal seed = 0;
          foreach (StockPrice p in seedWindow) seed += p.Close;
          seed /= Period;
          results.Add(seed);

          // EMA formula: (close - prevEma) * multiplier + prevEma
          for (int i = Period; i < arr.Length; i++)
          {
              decimal ema = (arr[i].Close - results[^1]) * multiplier + results[^1];
              results.Add(ema);
          }

          return results;
      }

      /// <summary>
      /// Signal: Buy if latest close is above the SMA, Sell if below.
      /// </summary>
      public override bool TryGetSignal(IReadOnlyList<StockPrice> prices, out SignalType signal)
      {
          signal = SignalType.None;
          if (prices.Count < Period) return false;

          IReadOnlyList<decimal> sma = Calculate(prices);
          decimal latestClose = prices[^1].Close;
          decimal latestSma   = sma[^1];

          signal = latestClose switch
          {
              var c when c > latestSma => SignalType.Buy,
              var c when c < latestSma => SignalType.Sell,
              _                        => SignalType.None
          };

          return signal != SignalType.None;
      }
  }
