using System;
using System.Collections.Generic;

namespace Sanctuary.Core.Collections;

public interface IWeighted
{
    int Weight { get; }
}

public sealed class WeightedDropTable<T> where T : IWeighted
{
    private readonly IReadOnlyList<T> _entries;

    public int TotalWeight { get; }

    public WeightedDropTable(IReadOnlyList<T> entries)
    {
        if (entries.Count == 0)
            throw new ArgumentException("A weighted drop table must contain at least one entry.", nameof(entries));

        long totalWeight = 0;

        foreach (var entry in entries)
        {
            if (entry.Weight <= 0)
                throw new ArgumentOutOfRangeException(nameof(entries), "Weighted drop table entries must have a positive weight.");

            totalWeight += entry.Weight;
        }

        if (totalWeight > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(entries), "Weighted drop table total weight overflows an int.");

        _entries = entries;
        TotalWeight = (int)totalWeight;
    }

    public T Select(int roll)
    {
        if (roll < 0 || roll >= TotalWeight)
            throw new ArgumentOutOfRangeException(nameof(roll));

        foreach (var entry in _entries)
        {
            if (roll < entry.Weight)
                return entry;

            roll -= entry.Weight;
        }

        throw new InvalidOperationException("The weighted drop table is invalid.");
    }

    public T SelectRandom(Random? random = null)
    {
        return Select((random ?? Random.Shared).Next(TotalWeight));
    }
}
