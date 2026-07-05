using System;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Game.Interactions;

public sealed class EnterHeweyEscapeInteraction : IInteraction
{
    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 11970,
        ButtonText = 102010,
        TooltipId = 382845
    };

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Npc npc || !IsHeweyEscapeMarker(npc))
            return;

        player.SendTunneled(new PacketChat
        {
            Channel = ChatChannel.System,
            Message = "Hewey's Escape entry marker selected."
        });
    }

    public static bool IsHeweyEscapeMarker(Npc npc)
    {
        return npc.ModelId == 91 &&
            string.Equals(npc.TextureAlias, "hewey", StringComparison.OrdinalIgnoreCase);
    }
}
