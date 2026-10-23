using System;
using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.QuestGoals;
using Sanctuary.Packet;

namespace Sanctuary.Game.Quests;

public static class QuestDialogue
{
    private const int YouGotItTextId = 103085;

    private const int PlusImageId = 303;
    private const int LeaveImageId = 4008;

    private const int GreenButtonImageSet = 17;

    public const int TalkAnimationId = 3105;

    private const int IdleAnimationId = 1;

    private const byte SetBaseAnimation = 1;

    private const int TalkAnimationMs = 1500;

    public static void PlayTalkAnimation(Player player, ulong npcGuid)
    {
        if (npcGuid == 0)
            return;

        if (player.QuestConversation.TalkingNpcGuid != 0 && player.QuestConversation.TalkingNpcGuid != npcGuid)
            StopTalkAnimation(player);

        player.QuestConversation.TalkingNpcGuid = npcGuid;
        player.QuestConversation.TalkAnimationEndsAt = DateTimeOffset.UtcNow.AddMilliseconds(TalkAnimationMs);

        player.SendTunneled(new PlayerUpdatePacketSetAnimation
        {
            Guid = npcGuid,
            AnimationId = TalkAnimationId,
            Flags = SetBaseAnimation
        });
    }

    public static void StopTalkAnimation(Player player)
    {
        if (player.QuestConversation.TalkingNpcGuid == 0)
            return;

        var npcGuid = player.QuestConversation.TalkingNpcGuid;
        player.QuestConversation.TalkingNpcGuid = 0;

        player.SendTunneled(new PlayerUpdatePacketSetAnimation
        {
            Guid = npcGuid,
            AnimationId = IdleAnimationId,
            Flags = SetBaseAnimation
        });
    }

    public static void Begin(Player player, IReadOnlyList<QuestDialogueLine> lines, ulong npcGuid)
    {
        player.QuestConversation.PendingLines.Clear();

        if (lines.Count == 0)
            return;

        player.QuestConversation.NpcGuid = npcGuid;

        for (var i = 1; i < lines.Count; i++)
            player.QuestConversation.PendingLines.Enqueue(lines[i]);

        Show(player, lines[0], npcGuid, isLastTurn: lines.Count == 1);
    }

    public static bool TryAdvance(Player player)
    {
        if (player.QuestConversation.PendingLines.Count == 0)
        {
            player.QuestConversation.NpcGuid = 0;
            StopTalkAnimation(player);
            return false;
        }

        var line = player.QuestConversation.PendingLines.Dequeue();

        Show(player, line, player.QuestConversation.NpcGuid, isLastTurn: player.QuestConversation.PendingLines.Count == 0);
        return true;
    }

    public static void Clear(Player player)
    {
        player.QuestConversation.PendingLines.Clear();
        player.QuestConversation.NpcGuid = 0;
        StopTalkAnimation(player);
    }

    private static void Show(Player player, QuestDialogueLine line, ulong npcGuid, bool isLastTurn)
    {
        var dialog = new CommandPacketShowDialog
        {
            DialogueTextId = line.TextId,
            NpcGuid = npcGuid,
            CameraFocusParam = 1f,
        };

        dialog.Responses.Add(new CommandPacketShowDialog.Response
        {
            Id = 1,
            LabelTextId = line.ResponseTextId != 0 ? line.ResponseTextId : YouGotItTextId,
            Param1 = isLastTurn ? LeaveImageId : PlusImageId,
            Param2 = GreenButtonImageSet,
        });

        PlayTalkAnimation(player, npcGuid);

        player.SendTunneled(dialog);
    }
}
