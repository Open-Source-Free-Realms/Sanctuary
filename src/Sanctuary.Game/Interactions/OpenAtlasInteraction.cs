using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public sealed class OpenAtlasInteraction : IInteraction
{
    public static readonly InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 1345,
        ButtonText = 7199 // Use Warpstone
    };

    public int Id => Data.Id;

    public static ExecuteScriptPacket? GetAtlasPacket(Npc stone, Player player)
    {
        if (!stone.OpensAtlas || !InteractionMenuHelper.CanInteract(stone, player))
            return null;

        // No map argument lets Atlas:Show use the player's current atlas.
        return new ExecuteScriptPacket { Script = "Atlas.Show" };
    }

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Npc stone)
            return;

        var executeScriptPacket = GetAtlasPacket(stone, player);
        if (executeScriptPacket is not null)
            player.SendTunneled(executeScriptPacket);
    }
}
