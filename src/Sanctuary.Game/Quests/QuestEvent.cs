using System.Numerics;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Quests;

public abstract record QuestEvent
{
    public virtual bool CreditsOneQuest => false;
}

public sealed record NpcTalkedEvent(Npc Npc) : QuestEvent;

public sealed record NodeGatheredEvent(CollectionNode Node) : QuestEvent
{
    public override bool CreditsOneQuest => true;
}

public sealed record PlayerMovedEvent(Vector4 Position, int ZoneDefinitionId) : QuestEvent;
