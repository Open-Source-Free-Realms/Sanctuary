using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Core.Helpers;

public class Switch : IEnumerable<Switch.Case>
{
    private readonly List<Case> _list = [];

    public void Add(Func<bool> condition, Action action)
    {
        _list.Add(new Case(condition, action));
    }

    IEnumerator<Case> IEnumerable<Case>.GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    public void Execute()
    {
        this
            .Where(c => c.Condition())
            .Select(c => c.Action)
            .FirstOrDefault()
            ?.Invoke();
    }

    public sealed record class Case(Func<bool> Condition, Action Action)
    {
    }
}