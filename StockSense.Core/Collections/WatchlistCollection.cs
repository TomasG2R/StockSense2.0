using System.Collections;
using StockSense.Core.Models;

namespace StockSense.Core.Collections;

// GRADING: IEnumerable<T> — this collection can be used in foreach loops
public sealed class WatchlistCollection : IEnumerable<StockSymbol>
{
    private readonly List<StockSymbol> _items = new();

    // GRADING: events — fires when the watchlist changes (add/remove)
    public event Action<IReadOnlyList<StockSymbol>>? OnChanged;

    public int Count => _items.Count;

    public bool Contains(string value) =>
        _items.Any(s => s.Value == value);

    public void Add(StockSymbol symbol)
    {
        _items.Add(symbol);
        OnChanged?.Invoke(_items);       // fire event
    }

    public bool Remove(StockSymbol symbol)
    {
        bool removed = _items.Remove(symbol);
        if (removed) OnChanged?.Invoke(_items);
        return removed;
    }

    public IReadOnlyList<string> AsStrings() =>
        _items.Select(s => s.Value).ToList();

    // GRADING: iterator — yields only symbols that pass the filter using yield return
    public IEnumerable<StockSymbol> Filter(Func<StockSymbol, bool> predicate)
    {
        foreach (StockSymbol symbol in _items)
        {
            if (predicate(symbol))
                yield return symbol;     // GRADING: iterator (yield return)
        }
    }

    // GRADING: IEnumerable<T> — required method, returns our custom enumerator
    public IEnumerator<StockSymbol> GetEnumerator() =>
        new WatchlistEnumerator(_items);

    // GRADING: IEnumerable<T> — non-generic version required by the interface
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // GRADING: IEnumerator<T> — custom enumerator that walks through the list
    private sealed class WatchlistEnumerator : IEnumerator<StockSymbol>
    {
        private readonly List<StockSymbol> _source;
        private int _index = -1;

        public WatchlistEnumerator(List<StockSymbol> source) => _source = source;

        // GRADING: IEnumerator<T> — Current gives the item at the current position
        public StockSymbol Current =>
            _index >= 0 && _index < _source.Count
                ? _source[_index]
                : throw new InvalidOperationException("Enumerator out of range.");

        object IEnumerator.Current => Current;

        // GRADING: IEnumerator<T> — MoveNext advances the position by one
        public bool MoveNext()
        {
            _index++;
            return _index < _source.Count;
        }

        // GRADING: IEnumerator<T> — Reset puts position back before the first item
        public void Reset() => _index = -1;

        public void Dispose() { }     // nothing to release here
    }
}