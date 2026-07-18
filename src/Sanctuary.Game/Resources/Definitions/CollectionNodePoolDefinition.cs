using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class CollectionNodePoolDefinition
{
    public required string Key { get; set; }
    public int ZoneDefinitionId { get; set; }
    public required string NodeType { get; set; }
    public int MaxActiveNodes { get; set; }
    public int RespawnSeconds { get; set; } = 30;

    public int GetTargetActiveCount(int hardPointCount)
    {
        if (hardPointCount < 0)
            throw new ArgumentOutOfRangeException(nameof(hardPointCount));

        // A zero cap enables every hard point while a pool is being authored.
        return MaxActiveNodes == 0 ? hardPointCount : Math.Min(MaxActiveNodes, hardPointCount);
    }

    public IReadOnlyList<CollectionNodeSpawnDefinition> SelectSpawnsToActivate(
        IEnumerable<CollectionNodeSpawnDefinition> hardPoints, IReadOnlySet<int> activeHardPointIds,
        int maximumToActivate, Random random, int? avoidHardPointId = null)
    {
        ArgumentNullException.ThrowIfNull(hardPoints);
        ArgumentNullException.ThrowIfNull(activeHardPointIds);
        ArgumentNullException.ThrowIfNull(random);

        if (maximumToActivate < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumToActivate));

        var candidates = hardPoints
            .Where(spawn => spawn.Pool == Key && !activeHardPointIds.Contains(spawn.Id))
            .ToList();
        var targetActiveCount = GetTargetActiveCount(activeHardPointIds.Count + candidates.Count);
        var selectionCount = Math.Min(maximumToActivate,
            Math.Max(0, targetActiveCount - activeHardPointIds.Count));
        var selected = new List<CollectionNodeSpawnDefinition>(selectionCount);

        // Prefer a different point after pickup, but never leave the pool below its target.
        if (avoidHardPointId.HasValue && candidates.Count > selectionCount)
            candidates.RemoveAll(spawn => spawn.Id == avoidHardPointId.Value);

        while (selected.Count < selectionCount && candidates.Count > 0)
        {
            var index = random.Next(candidates.Count);
            selected.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        return selected;
    }
}
