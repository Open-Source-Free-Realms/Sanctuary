using System;
using System.Collections.Generic;

namespace Sanctuary.Core.Collections;

public sealed class ConcurrentGroupedSet<TGroup, TValue>
    where TGroup : notnull
    where TValue : notnull
{
    private readonly object _gate = new();
    private readonly Dictionary<TGroup, HashSet<TValue>> _valuesByGroup = new();
    private volatile TValue[] _snapshot = [];

    public ReadOnlySpan<TValue> Snapshot => _snapshot;

    public bool TryAdd(TGroup group, TValue value)
    {
        lock (_gate)
        {
            if (!_valuesByGroup.TryGetValue(group, out var values))
            {
                values = [];
                _valuesByGroup.Add(group, values);
            }

            if (!values.Add(value))
                return false;

            UpdateSnapshot();
            return true;
        }
    }

    public bool TryRemove(TGroup group, TValue value)
    {
        lock (_gate)
        {
            if (!_valuesByGroup.TryGetValue(group, out var values) || !values.Remove(value))
                return false;

            if (values.Count == 0)
                _valuesByGroup.Remove(group);

            UpdateSnapshot();
            return true;
        }
    }

    public bool RemoveGroup(TGroup group)
    {
        lock (_gate)
        {
            if (!_valuesByGroup.Remove(group))
                return false;

            UpdateSnapshot();
            return true;
        }
    }

    private void UpdateSnapshot()
    {
        var count = 0;

        foreach (var values in _valuesByGroup.Values)
            count += values.Count;

        var snapshot = new TValue[count];
        var index = 0;

        foreach (var values in _valuesByGroup.Values)
        {
            values.CopyTo(snapshot, index);
            index += values.Count;
        }

        _snapshot = snapshot;
    }
}