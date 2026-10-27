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

    public static void Begin(Player player, IReadOnlyList<QuestDialogueLine> lines, ulong npcGuid, Action? onFinished = null)
    {
        player.QuestConversation.Start(lines, npcGuid, onFinished);

        if (player.QuestConversation.Current is null)
        {
            onFinished?.Invoke();
            return;
        }

        Show(player);
    }

    public static bool TryAdvance(Player player, int responseId)
    {
        var conversation = player.QuestConversation;

        if (conversation.Current is null)
            return false;

        switch (conversation.Answer(responseId))
        {
            case QuestConversationStep.Next:
                Show(player);
                return true;

            case QuestConversationStep.Finished:
                var onFinished = conversation.Finish();
                StopTalkAnimation(player);
                onFinished?.Invoke();
                return conversation.Current is not null;

            default:
                Clear(player);
                return false;
        }
    }

    public static void Clear(Player player)
    {
        player.QuestConversation.Clear();
        StopTalkAnimation(player);
    }

    private static void Show(Player player)
    {
        var conversation = player.QuestConversation;
        var line = conversation.Current!;

        var dialog = new CommandPacketShowDialog
        {
            DialogueTextId = line.TextId,
            NpcGuid = conversation.NpcGuid,
            Closable = line.Closable,
            CameraFocusParam = 1f,
        };

        if (line.Choices.Count > 0)
        {
            for (var i = 0; i < line.Choices.Count; i++)
            {
                dialog.Responses.Add(new CommandPacketShowDialog.Response
                {
                    Id = i + 1,
                    LabelTextId = line.Choices[i].ResponseTextId,
                    Param1 = PlusImageId,
                    Param2 = GreenButtonImageSet,
                });
            }
        }
        else
        {
            dialog.Responses.Add(new CommandPacketShowDialog.Response
            {
                Id = 1,
                LabelTextId = line.ResponseTextId != 0 ? line.ResponseTextId : YouGotItTextId,
                Param1 = conversation.IsLastLine ? LeaveImageId : PlusImageId,
                Param2 = GreenButtonImageSet,
            });
        }

        PlayTalkAnimation(player, conversation.NpcGuid);

        player.SendTunneled(dialog);
    }
}
