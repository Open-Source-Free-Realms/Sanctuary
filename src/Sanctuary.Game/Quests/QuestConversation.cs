using System;
using System.Collections.Generic;

using Sanctuary.Game.Resources.Definitions.QuestGoals;

namespace Sanctuary.Game.Quests;

public sealed class QuestConversation
{
    public Queue<QuestDialogueLine> PendingLines { get; } = new();

    public ulong NpcGuid { get; set; }

    public ulong TalkingNpcGuid { get; set; }

    public DateTimeOffset TalkAnimationEndsAt { get; set; }
}
