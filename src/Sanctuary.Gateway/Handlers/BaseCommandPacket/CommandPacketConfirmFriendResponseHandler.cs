using System;
using System.Diagnostics;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketConfirmFriendResponseHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketConfirmFriendResponseHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketConfirmFriendResponse.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketConfirmFriendResponse));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketConfirmFriendResponse), packet);

        if (!connection.Player.IncomingFriendRequests.Remove(packet.Guid))
            return true;

        if (!_zoneManager.TryGetPlayer(packet.Guid, out var player))
            return true;

        Debug.WriteLine($"Inviter: {player.Name.FullName}, Invitee: {connection.Player.Name.FullName}");

        var friendMessagePacket = new FriendMessagePacket();

        switch (packet.Status)
        {
            case 0:
                OnAccept(player, connection.Player);

                friendMessagePacket.Type = FriendMessageType.FriendAddRequestAccepted;
                break;

            case 1:
                friendMessagePacket.Type = FriendMessageType.FriendAddRequestDeclined;
                break;

            case 2:
                friendMessagePacket.Type = FriendMessageType.FriendAddRequestTimedOut;
                break;

            default:
                break;
        }

        friendMessagePacket.Guid = connection.Player.Guid;
        friendMessagePacket.Name = connection.Player.Name;

        player.SendTunneled(friendMessagePacket);

        return true;
    }

    private static void OnAccept(Player inviter, Player invitee)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var inviterDbCharacter = dbContext.Characters.SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(inviter.Guid));
        var inviteeDbCharacter = dbContext.Characters.SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(invitee.Guid));

        if (inviterDbCharacter is null || inviteeDbCharacter is null)
            return;

        inviterDbCharacter.Friends.Add(new DbFriend
        {
            FriendCharacterId = inviteeDbCharacter.Id
        });

        inviteeDbCharacter.Friends.Add(new DbFriend
        {
            FriendCharacterId = inviterDbCharacter.Id
        });

        if (dbContext.SaveChanges() <= 0)
            return;

        // Inviter
        {
            var inviterFriendData = new FriendData
            {
                Name = invitee.Name,
                Guid = invitee.Guid,

                Online = true,

                IsLocal = true,
                IsInStaticZone = true,

                Status =
                {
                    Status = 110,

                    ProfileId = invitee.ActiveProfile.Id,
                    ProfileRank = invitee.ActiveProfile.Rank,
                    ProfileIconId = invitee.ActiveProfile.Icon,
                    ProfileNameId = invitee.ActiveProfile.NameId,
                    ProfileBackgroundImageId = invitee.ActiveProfile.BadgeImageSet,
                }
            };

            inviter.Friends.Add(inviterFriendData);

            var inviterFriendAddPacket = new FriendAddPacket();

            inviterFriendAddPacket.Data = inviterFriendData;

            inviter.SendTunneled(inviterFriendAddPacket);
        }

        // Invitee
        {
            var inviteeFriendData = new FriendData
            {
                Name = inviter.Name,
                Guid = inviter.Guid,

                Online = true,

                IsLocal = true,
                IsInStaticZone = true,

                Status =
                {
                    Status = 110,

                    ProfileId = inviter.ActiveProfile.Id,
                    ProfileRank = inviter.ActiveProfile.Rank,
                    ProfileIconId = inviter.ActiveProfile.Icon,
                    ProfileNameId = inviter.ActiveProfile.NameId,
                    ProfileBackgroundImageId = inviter.ActiveProfile.BadgeImageSet,
                }
            };

            invitee.Friends.Add(inviteeFriendData);

            var inviteeFriendAddPacket = new FriendAddPacket();

            inviteeFriendAddPacket.Data = inviteeFriendData;

            invitee.SendTunneled(inviteeFriendAddPacket);
        }
    }
}