using System;
using System.Linq;
using System.Numerics;

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
public static class GuildInviteAcceptPacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildInviteAcceptPacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildInviteAcceptPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildInviteAcceptPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildInviteAcceptPacket), packet);

        if (!_zoneManager.TryGetPlayer(packet.PlayerGuid, out var player))
            return true;

        if (player.GuildData is null)
            return true;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters
            .SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbCharacter is null)
            return true;

        var dbGuild = dbContext.Guilds
            .SingleOrDefault(x => x.Id == player.GuildData.Guid);

        if (dbGuild is null)
            return true;

        var dbGuildMember = new DbGuildMember
        {
            Id = dbCharacter.Id,

            Role = GuildRole.Recruit.Id,

            Guild = dbGuild
        };

        dbContext.GuildMembers.Add(dbGuildMember);

        dbCharacter.GuildMemberId = dbGuildMember.Id;

        if (dbContext.SaveChanges() <= 0)
            return true;

        var guildData = player.GuildData;

        var memberGuid = GuidHelper.GetPlayerGuid(dbGuildMember.Id);

        guildData.Members.Add(memberGuid, new GuildMember
        {
            Guid = memberGuid,

            Role = dbGuildMember.Role,

            Name = connection.Player.Name,

            Online = true,

            WorldId = connection.Player.Zone.Id,

            ProfileId = connection.Player.ActiveProfileId,
            ProfileRank = connection.Player.ActiveProfile.Rank
        });

        connection.Player.GuildData = guildData;

        var guildDataFullPacket = new GuildDataFullPacket
        {
            Data = guildData,
            Guid = guildData.Guid
        };

        connection.SendTunneled(guildDataFullPacket);

        var guildPlayerStatusUpdatePacket = new GuildPlayerStatusUpdatePacket
        {
            PlayerGuid = connection.Player.Guid,
            GuildGuid = guildData.Guid,
            IsInGuild = true
        };

        connection.Player.SendTunneledToVisible(guildPlayerStatusUpdatePacket);

        var guildMemberStatusUpdatePacket = new GuildMemberStatusUpdatePacket
        {
            GuildGuid = guildData.Guid,
            MemberGuid = connection.Player.Guid,

            Name = connection.Player.Name,

            Role = dbGuildMember.Role,

            Online = true,

            Type = 1,

            WorldId = connection.Player.Zone.Id,

            ProfileId = connection.Player.ActiveProfileId,
            ProfileRank = connection.Player.ActiveProfile.Rank
        };

        player.SendTunneled(guildMemberStatusUpdatePacket);

        connection.Player.SendTunneled(guildMemberStatusUpdatePacket);

        player.SendTunneled(new GuildErrorPacket
        {
            MessageName = "GuildInviteAccepted"
        });

        return true;
    }
}