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

        if (connection.Player.GuildData is null || connection.Player.GuildData.Guid != packet.GuildGuid)
            return true;

        var demoterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var targetId = GuidHelper.GetPlayerId(packet.PlayerGuid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var demoterGuildMember = dbContext.GuildMembers
            .AsNoTracking()
            .SingleOrDefault(x => x.GuildId == packet.GuildGuid && x.Id == demoterId);

        var targetGuildMember = dbContext.GuildMembers
            .Include(x => x.Character)
            .SingleOrDefault(x => x.GuildId == packet.GuildGuid && x.Id == targetId);

        if (demoterGuildMember is null || targetGuildMember is null)
            return true;

        if (targetId == demoterId || !CanDemoteMember(demoterGuildMember.Role, targetGuildMember.Role))
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildPromoteCantPromoteAbove"
            });

            return true;
        }

        if (targetGuildMember.Role == GuildRole.Recruit.Id)
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildPromoteAtMinRank"
            });

            return true;
        }

        targetGuildMember.Role += 1;

        if (dbContext.SaveChanges() <= 0)
            return true;

        var memberName = new NameData
        {
            FirstName = targetGuildMember.Character.FirstName,
            LastName = targetGuildMember.Character.LastName ?? string.Empty
        };

        var online = _zoneManager.TryGetPlayer(packet.PlayerGuid, out var targetPlayer);
        var worldId = 0;
        var profileId = 0;
        var profileRank = 0;

        if (online)
        {
            memberName = targetPlayer!.Name;
            worldId = targetPlayer.Zone.Id;
            profileId = targetPlayer.ActiveProfileId;
            profileRank = targetPlayer.ActiveProfile.Rank;

            if (targetPlayer.GuildData?.Members.TryGetValue(packet.PlayerGuid, out var targetOnlineMember) == true)
                targetOnlineMember.Role = targetGuildMember.Role;
        }

        var guildMemberStatusUpdatePacket = new GuildMemberStatusUpdatePacket
        {
            GuildGuid = packet.GuildGuid,
            MemberGuid = packet.PlayerGuid,
            Name = memberName,
            Role = targetGuildMember.Role,
            Online = online,
            Type = 5,
            WorldId = worldId,
            ProfileId = profileId,
            ProfileRank = profileRank
        };

        foreach (var guildPlayer in _zoneManager.GetPlayers())
        {
            if (guildPlayer.GuildData is null || guildPlayer.GuildData.Guid != packet.GuildGuid)
                continue;

            if (guildPlayer.GuildData.Members.TryGetValue(packet.PlayerGuid, out var visibleMember))
                visibleMember.Role = targetGuildMember.Role;

            guildPlayer.SendTunneled(guildMemberStatusUpdatePacket);
        }

        return true;
    }

    private static bool CanDemoteMember(int actorRole, int targetRole)
    {
        if (actorRole == GuildRole.Leader.Id)
            return true;

        if (actorRole == GuildRole.Officer.Id)
            return targetRole > GuildRole.Officer.Id;

        return false;
    }
}
