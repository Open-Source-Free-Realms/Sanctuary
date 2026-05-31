using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

public class GuildInviteInteraction : IInteraction
{
    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 18435,
        ButtonText = 392996
    };

    public void OnInteract(Player player, IEntity other)
    {
        if (player.GuildData is null)
            return;

        if (player.GuildData.Members.Count >= player.GuildData.MaxMembers)
        {
            player.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildMemberCountExceeded"
            });

            return;
        }

        if (other is not Player otherPlayer)
        {
            player.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildInvitePlayerNotFound"
            });

            return;
        }

        if (otherPlayer.GuildData is not null)
        {
            player.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildInviteeInMaxGuilds"
            });

            return;
        }

        var guildInviteNotificationPacket = new GuildInviteNotificationPacket
        {
            GuildInvite =
            {
                FromPlayerGuid = player.Guid,

                InviterPlayerGuid = player.Guid,
                InviterName = player.Name,
            },
            GuildName = player.GuildData.Name
        };

        otherPlayer.SendTunneled(guildInviteNotificationPacket);

        player.SendTunneled(new GuildErrorPacket
        {
            MessageName = "GuildInviteSuccess"
        });
    }
}