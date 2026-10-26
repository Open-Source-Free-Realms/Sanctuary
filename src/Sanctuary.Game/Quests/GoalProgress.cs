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
