using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GuildQuitPacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildQuitPacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildQuitPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildQuitPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildQuitPacket), packet);

        if (connection.Player.GuildData is null)
            return true;

        if (connection.Player.GuildData.Guid != packet.Guid)
            return true;

        if (!connection.Player.GuildData.Members.TryGetValue(connection.Player.Guid, out var quitGuildMember))
            return true;

        if (quitGuildMember.Role == GuildRole.Leader.Id && connection.Player.GuildData.Members.Count > 1)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildPromoteAtMinRank"
            });

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbGuildMemberToRemove = dbContext.GuildMembers
            .Where(x => x.GuildId == packet.Guid && x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbGuildMemberToRemove.ExecuteDelete() <= 0)
            return true;

        var result = dbContext.Characters
            .Where(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid))
            .ExecuteUpdate(x => x.SetProperty(x => x.GuildMemberId, (ulong?)null));

        if (result <= 0)
            return true;

        var hasMembers = dbContext.GuildMembers.Any(m => m.GuildId == packet.Guid);

        if (!hasMembers)
        {
            var dbGuildToDelete = dbContext.Guilds
                .Where(g => g.Id == packet.Guid);

            if (dbGuildToDelete.ExecuteDelete() <= 0)
                return true;
        }

        var guildPlayerStatusUpdatePacket = new GuildPlayerStatusUpdatePacket
        {
            PlayerGuid = connection.Player.Guid,
            GuildGuid = packet.Guid,
            IsInGuild = false
        };

        connection.Player.SendTunneledToVisible(guildPlayerStatusUpdatePacket, true);

        var guildCanCreateGuildPacket = new GuildCanCreateGuildPacket
        {
            CanCreateGuild = connection.Player.Profiles.Any(x => x.Rank >= 15)
        };

        connection.SendTunneled(guildCanCreateGuildPacket);

        var guildMemberStatusUpdatePacket = new GuildMemberStatusUpdatePacket
        {
            GuildGuid = packet.Guid,
            MemberGuid = connection.Player.Guid,

            Type = 3
        };

        connection.SendTunneled(guildMemberStatusUpdatePacket);

        foreach (var guildMember in connection.Player.GuildData.Members)
        {
            if (!_zoneManager.TryGetPlayer(guildMember.Key, out var guildPlayer))
                continue;

            if (guildPlayer.GuildData is null)
                continue;

            guildPlayer.GuildData.Members.Remove(connection.Player.Guid);

            guildPlayer.SendTunneled(guildMemberStatusUpdatePacket);
        }

        connection.Player.GuildData = null;

        return true;
    }
}