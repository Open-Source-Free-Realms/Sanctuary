using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class InteractionMenuHelper
{
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
            commandPacketInteractionList.List.Interactions.Add(new InteractionData
            {
                Id = interaction.Id,
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

    public static PlayerUpdatePacketNpcRelevance GetNpcRelevancePacket(IEnumerable<Npc> npcs)
    {
        var playerUpdatePacketNpcRelevance = new PlayerUpdatePacketNpcRelevance();
        foreach (var npc in npcs)
        {
            if (npc is Mount || (npc.HasCursor is null && npc.CursorId == 0))
                continue;

            playerUpdatePacketNpcRelevance.Entries.Add(new PlayerUpdatePacketNpcRelevance.Entry
            {
                Guid = npc.Guid,
                HasCursor = npc.HasCursor ?? npc.CursorId != 0,
                CursorId = npc.CursorId,
                Unknown2 = npc.RelevanceUnknown2
            });
        }
        return playerUpdatePacketNpcRelevance;
    }

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

    public static bool CanInteract(Npc npc, Player player)
    {
        if (!npc.IsInteractable || !ReferenceEquals(npc.Zone, player.Zone))
            return false;

        var distanceSquared = Vector3.DistanceSquared(
            new Vector3(npc.Position.X, npc.Position.Y, npc.Position.Z),
            new Vector3(player.Position.X, player.Position.Y, player.Position.Z));
        return float.IsFinite(distanceSquared) && npc.InteractRange >= 0 &&
            distanceSquared <= (float)npc.InteractRange * npc.InteractRange;
    }
}
