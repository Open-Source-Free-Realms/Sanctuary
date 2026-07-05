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
        if (!CanInvite(player))
            return;

        var maxMembers = player.GuildData!.MaxMembers > 0 ? player.GuildData.MaxMembers : 100;
        if (player.GuildData.Members.Count >= maxMembers)
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
                InviteeName = otherPlayer.Name,
            },
            GuildName = player.GuildData.Name
        };

        otherPlayer.SendTunneled(guildInviteNotificationPacket);

        player.SendTunneled(new GuildErrorPacket
        {
            MessageName = "GuildInviteSuccess"
        });
    }

    public static bool CanInvite(Player player)
    {
        if (player.GuildData is null)
            return false;

        if (!player.GuildData.Members.TryGetValue(player.Guid, out var guildMember))
            return false;

        return guildMember.Role is 1 or 2;
    }
}
