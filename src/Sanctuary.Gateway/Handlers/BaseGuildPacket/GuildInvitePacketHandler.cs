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

        using var dbContext = _dbContextFactory.CreateDbContext();

        var guildGuid = connection.Player.GuildData.Guid;
        var deletedOrphanedMembers = dbContext.GuildMembers
            .Where(x => x.GuildId == guildGuid && !dbContext.Characters.Any(c => c.Id == x.Id))
            .ExecuteDelete();

        if (deletedOrphanedMembers > 0)
        {
            _logger.LogWarning(
                "Deleted orphaned guild members before invite. GuildGuid: {guildGuid}, DeletedMembers: {deletedMembers}",
                guildGuid,
                deletedOrphanedMembers);
        }

        var dbGuild = dbContext.Guilds
            .Include(x => x.Members)
            .SingleOrDefault(x => x.Id == guildGuid);

        if (dbGuild is null)
            return true;

        var inviterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var inviterRole = dbContext.GuildMembers
            .AsNoTracking()
            .Where(x => x.GuildId == guildGuid && x.Id == inviterId)
            .Select(x => (int?)x.Role)
            .SingleOrDefault();

        if (!CanInvite(inviterRole))
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildPromoteCantPromoteAbove"
            });

            return true;
        }

        var maxMembers = dbGuild.MaxMembers > 0 ? dbGuild.MaxMembers : 100;
        if (dbGuild.Members.Count >= maxMembers)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildMemberCountExceeded"
            });

            return true;
        }

        var dbCharacter = FindInvitee(dbContext, packet, guildGuid);

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

    private static DbCharacter? FindInvitee(DatabaseContext dbContext, GuildInvitePacket packet, ulong currentGuildGuid)
    {
        var candidateGuid = packet.PlayerGuid;

        if (candidateGuid == currentGuildGuid && packet.GuildGuid > 0)
            candidateGuid = packet.GuildGuid;

        if (candidateGuid > 0)
        {
            try
            {
                var characterId = GuidHelper.GetPlayerId(candidateGuid);
                var byGuid = dbContext.Characters.SingleOrDefault(x => x.Id == characterId);

                if (byGuid is not null)
                    return byGuid;
            }
            catch (ArgumentOutOfRangeException)
            {
                var byRawId = dbContext.Characters.SingleOrDefault(x => x.Id == candidateGuid);

                if (byRawId is not null)
                    return byRawId;
            }
        }

        var playerName = NormalizeName(packet.PlayerName);

        if (string.IsNullOrWhiteSpace(playerName))
            return null;

        var normalizedPlayerName = playerName.ToLower();
        return dbContext.Characters.SingleOrDefault(x => x.FullName != null && x.FullName.ToLower() == normalizedPlayerName);
    }

    private static string NormalizeName(string? name)
    {
        return string.Join(' ', (name ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool CanInvite(int? role)
    {
        return role is 1 or 2;
    }
}
