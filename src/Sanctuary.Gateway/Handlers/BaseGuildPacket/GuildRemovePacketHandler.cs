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

        if (connection.Player.GuildData is null || connection.Player.GuildData.Guid != packet.GuildGuid)
            return true;

        var removerId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var targetId = GuidHelper.GetPlayerId(packet.PlayerGuid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var removerGuildMember = dbContext.GuildMembers
            .AsNoTracking()
            .SingleOrDefault(x => x.GuildId == packet.GuildGuid && x.Id == removerId);

        var targetGuildMember = dbContext.GuildMembers
            .AsNoTracking()
            .SingleOrDefault(x => x.GuildId == packet.GuildGuid && x.Id == targetId);

        if (removerGuildMember is null || targetGuildMember is null)
            return true;

        if (targetId == removerId || !CanManageMember(removerGuildMember.Role, targetGuildMember.Role))
        {
            connection.SendTunneled(new GuildErrorPacket
            {
                MessageName = "GuildPromoteCantPromoteAbove"
            });

            return true;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var committed = strategy.Execute(() =>
        {
            using var transaction = dbContext.Database.BeginTransaction();

            dbContext.Characters
                .Where(x => x.Id == targetId)
                .ExecuteUpdate(x => x.SetProperty(x => x.GuildMemberId, (ulong?)null));

            var deleted = dbContext.GuildMembers
                .Where(x => x.GuildId == packet.GuildGuid && x.Id == targetId)
                .ExecuteDelete();

            if (deleted <= 0)
                return false;

            transaction.Commit();
            return true;
        });

        if (!committed)
            return true;

        var guildMemberStatusUpdatePacket = new GuildMemberStatusUpdatePacket
        {
            GuildGuid = packet.GuildGuid,
            MemberGuid = packet.PlayerGuid,
            Type = 2
        };

        foreach (var member in connection.Player.GuildData.Members.Values.ToList())
        {
            if (!_zoneManager.TryGetPlayer(member.Guid, out var guildPlayer))
                continue;

            if (guildPlayer.GuildData is null || guildPlayer.GuildData.Guid != packet.GuildGuid)
                continue;

            guildPlayer.GuildData.Members.Remove(packet.PlayerGuid);

            if (guildPlayer.Guid == packet.PlayerGuid)
                continue;

            guildPlayer.SendTunneled(guildMemberStatusUpdatePacket);
        }

        connection.Player.GuildData.Members.Remove(packet.PlayerGuid);

        if (!_zoneManager.TryGetPlayer(packet.PlayerGuid, out var player))
            return true;

        var guildCanCreateGuildPacket = new GuildCanCreateGuildPacket
        {
            CanCreateGuild = player.Profiles.Any(x => x.Rank >= 15)
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

    private static bool CanManageMember(int actorRole, int targetRole)
    {
        if (actorRole == GuildRole.Leader.Id)
            return true;

        if (actorRole == GuildRole.Officer.Id)
            return targetRole > GuildRole.Officer.Id;

        return false;
    }
}
