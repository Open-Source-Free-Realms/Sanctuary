using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public class AddFriendInteraction : IInteraction
{
    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 134,
        ButtonText = 3370
    };

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Player otherPlayer)
            return;

        if (otherPlayer.Ignores.Any(x => x.Guid == player.Guid))
            return;

        otherPlayer.IncomingFriendRequests.Add(player.Guid);

        var friendMessagePacket = new FriendMessagePacket();

        friendMessagePacket.Type = FriendMessageType.FriendAddRequested;

        friendMessagePacket.Guid = otherPlayer.Guid;
        friendMessagePacket.Name = otherPlayer.Name;

        player.SendTunneled(friendMessagePacket);

        var commandPacketConfirmFriendRequest = new CommandPacketConfirmFriendRequest();

        commandPacketConfirmFriendRequest.Guid = player.Guid;
        commandPacketConfirmFriendRequest.Name = player.Name;

        otherPlayer.SendTunneled(commandPacketConfirmFriendRequest);
    }
}