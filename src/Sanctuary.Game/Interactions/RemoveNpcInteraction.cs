using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public sealed class RemoveNpcInteraction : IInteraction
{
    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 135,
        ButtonText = 18704
    };

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Npc npc)
            return;

        if (!player.IsAdmin)
            return;

        npc.Dispose();
    }
}
