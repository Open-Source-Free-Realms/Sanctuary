using System;
using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Interactions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class InteractionMenuHelper
{
    public const byte QuestCursorId = 5;

    public static CommandPacketInteractionList GetInteractionListPacket(ulong guid, string? name,
        IEnumerable<InteractionData> interactions, bool autoSelectSingle = false)
    {
        var commandPacketInteractionList = new CommandPacketInteractionList();
        commandPacketInteractionList.List.Guid = guid;
        commandPacketInteractionList.List.Name = name ?? string.Empty;
        // The client skips the box for a single option when this flag is true.
        commandPacketInteractionList.List.Unknown = autoSelectSingle;
        commandPacketInteractionList.List.Unknown2 = true;
        foreach (var interaction in interactions)
        {
            var id = interaction.Type switch
            {
                MerchantInteraction.Type => MerchantInteraction.Data.Id,
                SnowballInteraction.Type => SnowballInteraction.Data.Id,
                _ => interaction.Id
            };
            commandPacketInteractionList.List.Interactions.Add(new InteractionData
            {
                Id = id,
                IconId = interaction.IconId,
                ButtonText = interaction.ButtonText,
                Type = interaction.Type,
                Param1 = interaction.Param1,
                Param2 = interaction.Param2,
                DescString = interaction.DescString,
                TooltipId = interaction.TooltipId
            });
        }
        return commandPacketInteractionList;
    }

    public static CommandPacketInteractionList GetInteractionListPacket(ulong guid, InteractionList list, bool unknown = false)
    {
        var commandPacketInteractionList = GetInteractionListPacket(guid, list.Name, list.Interactions, list.Unknown);
        commandPacketInteractionList.List.Unknown2 = list.Unknown2;
        commandPacketInteractionList.Unknown = unknown;
        return commandPacketInteractionList;
    }

    public static PlayerUpdatePacketNpcRelevance GetNpcRelevancePacket(IEnumerable<Npc> npcs, Func<Npc, bool>? hasQuestCursor = null)
    {
        var playerUpdatePacketNpcRelevance = new PlayerUpdatePacketNpcRelevance();
        foreach (var npc in npcs)
        {
            if (npc is Mount)
                continue;

            if (hasQuestCursor?.Invoke(npc) == true)
                playerUpdatePacketNpcRelevance.Entries.Add(GetQuestCursorEntry(npc.Guid));
            else if (HasOwnCursor(npc))
                playerUpdatePacketNpcRelevance.Entries.Add(GetOwnCursorEntry(npc));
        }
        return playerUpdatePacketNpcRelevance;
    }

    public static bool HasOwnCursor(Npc npc) => npc.HasCursor is not null || npc.CursorId != 0;

    public static PlayerUpdatePacketNpcRelevance.Entry GetQuestCursorEntry(ulong guid) => new()
    {
        Guid = guid,
        HasCursor = true,
        CursorId = QuestCursorId
    };

    public static PlayerUpdatePacketNpcRelevance.Entry GetOwnCursorEntry(Npc npc) => new()
    {
        Guid = npc.Guid,
        HasCursor = npc.HasCursor ?? npc.CursorId != 0,
        CursorId = npc.CursorId,
        Unknown2 = npc.RelevanceUnknown2
    };

    public static NotificationInfo GetNotification(ulong guid, NotificationInfo definition) => new()
    {
        Guid = guid,
        IsCompact = definition.IsCompact,
        NotificationType = definition.NotificationType,
        IconId = definition.IconId,
        IconState = definition.IconState,
        Unknown = definition.Unknown,
        NameId = definition.NameId,
        ReferenceId = definition.ReferenceId,
        Unknown2 = definition.Unknown2,
        Unknown3 = definition.Unknown3,
        Enabled = definition.Enabled
    };

    public static bool TryOpenMenu(Player player, Npc npc, IReadOnlyList<NpcInteractionOption> options)
    {
        if (options.Count == 0)
            return false;

        if (!CanInteract(npc, player))
            return true;

        if (npc.QuestInteractRange > 0 && !IsWithinRange(npc, player, npc.QuestInteractRange))
        {
            player.SendTunneled(new CommandPacketMoveAndInteract
            {
                Position = npc.Position,
                Guid = npc.Guid
            });
            return true;
        }

        var npcInteractions = npc.InteractionList?.Interactions ?? npc.Interactions;

        if (options.Count == 1 && npcInteractions.Count == 0)
        {
            options[0].Invoke(player);
            return true;
        }

        player.SendInteractionMenu(npc, options, npcInteractions);
        return true;
    }

    public static bool CanInteract(Npc npc, Player player)
    {
        if (!npc.IsInteractable || !ReferenceEquals(npc.Zone, player.Zone))
            return false;

        return npc.InteractRange >= 0 && IsWithinRange(npc, player, npc.InteractRange);
    }

    private static bool IsWithinRange(Npc npc, Player player, int range)
    {
        var distanceSquared = Vector3.DistanceSquared(
            npc.Position.ToVector3(),
            player.Position.ToVector3());
        return float.IsFinite(distanceSquared) && distanceSquared <= (float)range * range;
    }
}
