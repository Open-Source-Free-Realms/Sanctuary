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
public static class GuildPromotePacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildPromotePacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildPromotePacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildPromotePacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildPromotePacket), packet);

        if (connection.Player.GuildData is null)
            return true;

        if (!connection.Player.GuildData.Members.TryGetValue(connection.Player.Guid, out var promoterGuildMember))
            return true;

        if (!_zoneManager.TryGetPlayer(packet.PlayerGuid, out var promotePlayer))
            return true;

        if (promotePlayer.GuildData is null)
            return true;

        if (!promotePlayer.GuildData.Members.TryGetValue(promotePlayer.Guid, out var promoteGuildMember))
            return true;

        if (promoterGuildMember.Role >= promoteGuildMember.Role)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildPromoteCantPromoteAbove"
            });

            return true;
        }

        if (promoteGuildMember.Role == GuildRole.Leader.Id)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildPromoteAtMaxRank"
            });

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbGuildMember = dbContext.GuildMembers
            .SingleOrDefault(x => x.GuildId == packet.GuildGuid && x.Id == GuidHelper.GetPlayerId(packet.PlayerGuid));

        if (dbGuildMember is null)
            return true;

        dbGuildMember.Role -= 1;

        if (dbContext.SaveChanges() <= 0)
            return true;

        promoteGuildMember.Role -= 1;

        var guildMemberStatusUpdatePacket = new GuildMemberStatusUpdatePacket
        {
            GuildGuid = packet.GuildGuid,
            MemberGuid = packet.PlayerGuid,

            Name = promotePlayer.Name,

            Role = promoteGuildMember.Role,

            Online = true,

            Type = 4,

            WorldId = promotePlayer.Zone.Id,

            ProfileId = promotePlayer.ActiveProfileId,
            ProfileRank = promotePlayer.ActiveProfile.Rank
        };

        foreach (var guildMember in connection.Player.GuildData.Members)
        {
            if (!_zoneManager.TryGetPlayer(guildMember.Key, out var guildPlayer))
                continue;

            if (guildPlayer.GuildData is null)
                continue;

            guildPlayer.SendTunneled(guildMemberStatusUpdatePacket);
        }

        return true;
    }
}