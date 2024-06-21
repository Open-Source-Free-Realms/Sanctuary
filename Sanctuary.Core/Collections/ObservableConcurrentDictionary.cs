using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace Sanctuary.Core.Collections;

[DebuggerDisplay("Count={Count}")]
public class ObservableConcurrentDictionary<TKey, TValue> : IDictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged where TKey : notnull
{
    private readonly SynchronizationContext _context;
    private readonly ConcurrentDictionary<TKey, TValue> _dictionary;

    /// <summary>
    /// Initializes an instance of the ObservableConcurrentDictionary class.
    /// </summary>
    public ObservableConcurrentDictionary()
    {
        _context = AsyncOperationManager.SynchronizationContext;
        _dictionary = new ConcurrentDictionary<TKey, TValue>();
    }

    /// <summary>Event raised when a property on the collection changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Event raised when the collection changes.</summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// Notifies observers of CollectionChanged or PropertyChanged of an update to the dictionary.
    /// </summary>
    private void NotifyObserversOfChange()
    {
        var propertyHandler = PropertyChanged;
        var collectionHandler = CollectionChanged;

        _context.Post(s =>
        {
            collectionHandler?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            propertyHandler?.Invoke(this, new PropertyChangedEventArgs("Count"));
            propertyHandler?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            propertyHandler?.Invoke(this, new PropertyChangedEventArgs(nameof(Keys)));
            propertyHandler?.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
        }, null);
    }

    /// <summary>Attempts to add an item to the dictionary, notifying observers of any changes.</summary>
    /// <param name="item">The item to be added.</param>
    /// <returns>Whether the add was successful.</returns>
    private bool TryAddWithNotification(KeyValuePair<TKey, TValue> item) => TryAddWithNotification(item.Key, item.Value);

    /// <summary>Attempts to add an item to the dictionary, notifying observers of any changes.</summary>
    /// <param name="key">The key of the item to be added.</param>
    /// <param name="value">The value of the item to be added.</param>
    /// <returns>Whether the add was successful.</returns>
    private bool TryAddWithNotification(TKey key, TValue value)
    {
        var result = _dictionary.TryAdd(key, value);

        if (result)
            NotifyObserversOfChange();

        return result;
    }

    /// <summary>Attempts to remove an item from the dictionary, notifying observers of any changes.</summary>
    /// <param name="key">The key of the item to be removed.</param>
    /// <param name="value">The value of the item removed.</param>
    /// <returns>Whether the removal was successful.</returns>
    private bool TryRemoveWithNotification(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var result = _dictionary.TryRemove(key, out value);

        if (result)
            NotifyObserversOfChange();

        return result;
    }

    /// <summary>Attempts to add or update an item in the dictionary, notifying observers of any changes.</summary>
    /// <param name="key">The key of the item to be updated.</param>
    /// <param name="value">The new value to set for the item.</param>
    /// <returns>Whether the update was successful.</returns>
    private void UpdateWithNotification(TKey key, TValue value)
    {
        _dictionary[key] = value;
        NotifyObserversOfChange();
    }

    #region ICollection<KeyValuePair<TKey, TValue>> Members

    public void Add(KeyValuePair<TKey, TValue> item) => TryAddWithNotification(item);

    public void Clear()
    {
        ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Clear();
        NotifyObserversOfChange();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Contains(item);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).CopyTo(array, arrayIndex);

    public int Count => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Count;

    public bool IsReadOnly => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).IsReadOnly;

    public bool Remove(KeyValuePair<TKey, TValue> item) => TryRemoveWithNotification(item.Key, out _);

    #endregion

    #region IEnumerable<KeyValuePair<TKey, TValue>> Members

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => _dictionary.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();

    #endregion

    #region IDictionary<TKey, TValue> Members

    public void Add(TKey key, TValue value) => TryAddWithNotification(key, value);

    public bool TryAdd(TKey key, TValue value) => TryAddWithNotification(key, value);

    public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

    public ICollection<TKey> Keys => _dictionary.Keys;

    public bool Remove(TKey key) => TryRemoveWithNotification(key, out _);

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _dictionary.TryGetValue(key, out value);

    public ICollection<TValue> Values => _dictionary.Values;

    public TValue this[TKey key]
    {
        get => _dictionary[key];
        set => UpdateWithNotification(key, value);
    }

    #endregion
}