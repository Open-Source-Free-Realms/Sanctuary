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
public static class GuildRemovePacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildRemovePacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildRemovePacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildRemovePacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildRemovePacket), packet);

        if (connection.Player.GuildData is null)
            return true;

        if (connection.Player.GuildData.Guid != packet.GuildGuid)
            return true;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbGuildMemberToRemove = dbContext.GuildMembers
            .Where(x => x.GuildId == packet.GuildGuid && x.Id == GuidHelper.GetPlayerId(packet.PlayerGuid));

        if (dbGuildMemberToRemove.ExecuteDelete() <= 0)
            return true;

        var result = dbContext.Characters
            .Where(x => x.Id == GuidHelper.GetPlayerId(packet.PlayerGuid))
            .ExecuteUpdate(x => x.SetProperty(x => x.GuildMemberId, (ulong?)null));

        if (result <= 0)
            return true;

        var guildMemberStatusUpdatePacket = new GuildMemberStatusUpdatePacket
        {
            GuildGuid = packet.GuildGuid,
            MemberGuid = packet.PlayerGuid,

            Type = 2
        };

        foreach (var guildMember in connection.Player.GuildData.Members)
        {
            if (guildMember.Key == packet.PlayerGuid)
                continue;

            if (!_zoneManager.TryGetPlayer(guildMember.Key, out var guildPlayer))
                continue;

            if (guildPlayer.GuildData is null)
                continue;

            guildPlayer.GuildData.Members.Remove(packet.PlayerGuid);

            guildPlayer.SendTunneled(guildMemberStatusUpdatePacket);
        }

        if (!_zoneManager.TryGetPlayer(packet.PlayerGuid, out var player))
            return true;

        var guildCanCreateGuildPacket = new GuildCanCreateGuildPacket
        {
            CanCreateGuild = connection.Player.Profiles.Any(x => x.Rank >= 15)
        };

        player.SendTunneled(guildCanCreateGuildPacket);

        var guildPlayerStatusUpdatePacket = new GuildPlayerStatusUpdatePacket
        {
            PlayerGuid = packet.PlayerGuid,
            GuildGuid = packet.GuildGuid,
            IsInGuild = false
        };

        player.SendTunneledToVisible(guildPlayerStatusUpdatePacket, true);

        player.GuildData = null;

        return true;
    }
}