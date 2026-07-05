using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Sanctuary.Core.Collections;

public class ConcurrentSet<T> : IEnumerable<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dictionary;

    /// <summary>
    /// Initializes an instance of the ConcurrentSet class.
    /// </summary>
    public ConcurrentSet()
    {
        _dictionary = new ConcurrentDictionary<T, byte>();
    }

    public bool TryAdd(T item) => _dictionary.TryAdd(item, 0);

    public bool Contains(T item) => _dictionary.ContainsKey(item);

    public bool TryRemove(T item) => _dictionary.TryRemove(item, out _);

    public void Clear() => _dictionary.Clear();

    public int Count => _dictionary.Count;

    public bool IsEmpty => _dictionary.IsEmpty;

    public IEnumerator<T> GetEnumerator() => _dictionary.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
