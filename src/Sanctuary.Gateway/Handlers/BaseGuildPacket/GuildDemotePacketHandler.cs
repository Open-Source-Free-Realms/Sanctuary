using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GuildDemotePacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildDemotePacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildDemotePacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildDemotePacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildDemotePacket), packet);

        if (connection.Player.GuildData is null)
            return true;

        if (!connection.Player.GuildData.Members.TryGetValue(connection.Player.Guid, out var demoterGuildMember))
            return true;

        if (!_zoneManager.TryGetPlayer(packet.PlayerGuid, out var demotePlayer))
            return true;

        if (demotePlayer.GuildData is null)
            return true;

        if (!demotePlayer.GuildData.Members.TryGetValue(demotePlayer.Guid, out var demoteGuildMember))
            return true;

        if (demoterGuildMember.Role >= demoteGuildMember.Role)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildPromoteCantPromoteAbove"
            });

            return true;
        }

        if (demoteGuildMember.Role == GuildRole.Recruit.Id)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildPromoteAtMinRank"
            });

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbGuildMember = dbContext.GuildMembers
            .SingleOrDefault(x => x.GuildId == packet.GuildGuid && x.Id == GuidHelper.GetPlayerId(packet.PlayerGuid));

        if (dbGuildMember is null)
            return true;

        dbGuildMember.Role += 1;

        if (dbContext.SaveChanges() <= 0)
            return true;

        demoteGuildMember.Role += 1;

        var guildMemberStatusUpdatePacket = new GuildMemberStatusUpdatePacket
        {
            GuildGuid = packet.GuildGuid,
            MemberGuid = packet.PlayerGuid,

            Name = demotePlayer.Name,

            Role = demoteGuildMember.Role,

            Online = true,

            Type = 5,

            WorldId = demotePlayer.Zone.Id,

            ProfileId = demotePlayer.ActiveProfileId,
            ProfileRank = demotePlayer.ActiveProfile.Rank
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