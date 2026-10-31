using Sanctuary.Game.Entities;
using Sanctuary.Game.Groups;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public class GroupInviteInteraction : IInteraction
{
    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 136,
        ButtonText = 3372
    };

    private readonly IGroupManager _groupManager;

    public GroupInviteInteraction(IGroupManager groupManager)
    {
        _groupManager = groupManager;
    }

    public void OnInteract(Player player, IEntity other)
    {
        _groupManager.Invite(player, other as Player);
    }
}
