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

        using var dbContext = _dbContextFactory.CreateDbContext();
        using var transaction = dbContext.Database.BeginTransaction();

        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var quitRole = dbContext.GuildMembers
            .AsNoTracking()
            .Where(x => x.GuildId == packet.Guid && x.Id == characterId)
            .Select(x => (int?)x.Role)
            .SingleOrDefault();

        if (quitRole is null)
            return true;

        var result = dbContext.Characters
            .Where(x => x.Id == characterId)
            .ExecuteUpdate(x => x.SetProperty(x => x.GuildMemberId, (ulong?)null));

        if (result <= 0)
            return true;

        var dbGuildMemberToRemove = dbContext.GuildMembers
            .Where(x => x.GuildId == packet.Guid && x.Id == characterId);

        if (dbGuildMemberToRemove.ExecuteDelete() <= 0)
            return true;

        var hasMembers = dbContext.GuildMembers.Any(m => m.GuildId == packet.Guid);
        GuildMemberStatusUpdatePacket? promotedLeaderStatusUpdatePacket = null;

        if (!hasMembers)
        {
            var dbGuildToDelete = dbContext.Guilds
                .Where(g => g.Id == packet.Guid);

            if (dbGuildToDelete.ExecuteDelete() <= 0)
                return true;
        }
        else if (quitRole == GuildRole.Leader.Id
                 && !dbContext.GuildMembers.Any(m => m.GuildId == packet.Guid && m.Role == GuildRole.Leader.Id))
        {
            var newLeader = dbContext.GuildMembers
                .Include(x => x.Character)
                .Where(x => x.GuildId == packet.Guid)
                .AsEnumerable()
                .OrderBy(x => x.Role)
                .ThenBy(x => x.Id)
                .FirstOrDefault();

            if (newLeader is not null)
            {
                newLeader.Role = GuildRole.Leader.Id;

                if (dbContext.SaveChanges() <= 0)
                    return true;

                var newLeaderGuid = GuidHelper.GetPlayerGuid(newLeader.Id);
                var memberName = new NameData
                {
                    FirstName = newLeader.Character.FirstName,
                    LastName = newLeader.Character.LastName ?? string.Empty
                };

                var online = _zoneManager.TryGetPlayer(newLeaderGuid, out var newLeaderPlayer);
                var worldId = 0;
                var profileId = 0;
                var profileRank = 0;

                if (online)
                {
                    memberName = newLeaderPlayer!.Name;
                    worldId = newLeaderPlayer.Zone.Id;
                    profileId = newLeaderPlayer.ActiveProfileId;
                    profileRank = newLeaderPlayer.ActiveProfile.Rank;
                }

                promotedLeaderStatusUpdatePacket = new GuildMemberStatusUpdatePacket
                {
                    GuildGuid = packet.Guid,
                    MemberGuid = newLeaderGuid,
                    Name = memberName,
                    Role = GuildRole.Leader.Id,
                    Online = online,
                    Type = 4,
                    WorldId = worldId,
                    ProfileId = profileId,
                    ProfileRank = profileRank
                };
            }
        }

        transaction.Commit();

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

        foreach (var guildPlayer in _zoneManager.GetPlayers())
        {
            if (guildPlayer.GuildData is null || guildPlayer.GuildData.Guid != packet.Guid)
                continue;

            guildPlayer.GuildData.Members.Remove(connection.Player.Guid);

            if (guildPlayer.Guid == connection.Player.Guid)
                continue;

            guildPlayer.SendTunneled(guildMemberStatusUpdatePacket);

            if (promotedLeaderStatusUpdatePacket is null)
                continue;

            if (guildPlayer.GuildData.Members.TryGetValue(promotedLeaderStatusUpdatePacket.MemberGuid, out var promotedMember))
                promotedMember.Role = promotedLeaderStatusUpdatePacket.Role;

            guildPlayer.SendTunneled(promotedLeaderStatusUpdatePacket);
        }

        connection.Player.GuildData = null;

        return true;
    }
}