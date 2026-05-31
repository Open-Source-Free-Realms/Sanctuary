using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GuildInvitePacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildInvitePacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildInvitePacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildInvitePacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildInvitePacket), packet);

        if (connection.Player.GuildData is null)
            return true;

        if (connection.Player.GuildData.Members.Count >= connection.Player.GuildData.MaxMembers)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildMemberCountExceeded"
            });

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        DbCharacter? dbCharacter = null;

        if (!string.IsNullOrEmpty(packet.PlayerName))
        {
            dbCharacter = dbContext.Characters.SingleOrDefault(x => x.FullName == packet.PlayerName);
        }
        else if (packet.PlayerGuid > 0)
        {
            dbCharacter = dbContext.Characters.SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(packet.PlayerGuid));
        }

        if (dbCharacter is null)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildInvitePlayerNotFound"
            });

            return true;
        }

        if (dbCharacter.GuildMemberId > 0)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildInviteeInMaxGuilds"
            });

            return true;
        }

        if (!_zoneManager.TryGetPlayer(GuidHelper.GetPlayerGuid(dbCharacter.Id), out var player))
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildInvitePlayerNotFound"
            });

            return true;
        }

        var guildInviteNotificationPacket = new GuildInviteNotificationPacket
        {
            GuildInvite =
            {
                FromPlayerGuid = connection.Player.Guid,

                InviterPlayerGuid = connection.Player.Guid,
                InviterName = connection.Player.Name,
            },
            GuildName = connection.Player.GuildData.Name
        };

        player.SendTunneled(guildInviteNotificationPacket);

        connection.SendTunneled(new GuildErrorPacket
        {
            MessageName = "GuildInviteSuccess"
        });

        return true;
    }
}