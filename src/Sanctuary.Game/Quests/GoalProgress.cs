using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Quests;

public class GoalProgress
{
    public int Count { get; protected set; }

    public void Increment() => Count++;

    public virtual IEnumerable<(ulong TargetId, int Count)> Save()
    {
        if (Count > 0)
            yield return (0, Count);
    }

    public virtual void Load(ulong targetId, int count) => Count += count;
}

public sealed class TalkProgress : GoalProgress
{
    private readonly HashSet<int> _talkedNpcIds = [];

    public bool HasTalkedTo(int npcId) => _talkedNpcIds.Contains(npcId);

    public bool TryAdd(int npcId)
    {
        if (!_talkedNpcIds.Add(npcId))
            return false;

        Count++;
        return true;
    }

    public override IEnumerable<(ulong TargetId, int Count)> Save() => _talkedNpcIds.Select(npcId => ((ulong)npcId, 1));

    public override void Load(ulong targetId, int count)
    {
        if (targetId != 0)
            TryAdd((int)targetId);
    }
}

public sealed class CollectProgress : GoalProgress
{
    private readonly HashSet<int> _gatheredSpawnIds = [];

    public bool HasGathered(int spawnId) => _gatheredSpawnIds.Contains(spawnId);

    public bool TryAdd(int spawnId)
    {
        if (!_gatheredSpawnIds.Add(spawnId))
            return false;

        Count++;
        return true;
    }

    public override IEnumerable<(ulong TargetId, int Count)> Save()
        => _gatheredSpawnIds.Count > 0 ? _gatheredSpawnIds.Select(spawnId => ((ulong)spawnId, 1)) : base.Save();

    public override void Load(ulong targetId, int count)
    {
        if (targetId != 0)
            TryAdd((int)targetId);
        else
            base.Load(targetId, count);
    }
}
