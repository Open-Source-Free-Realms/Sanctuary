using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Gateway.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GuildRenameGuildPacketHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildRenameGuildPacketHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildRenameGuildPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildRenameGuildPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildRenameGuildPacket), packet);

        if (connection.Player.GuildData is null)
            return true;

        var guildGuid = connection.Player.GuildData.Guid;
        var guildName = GuildHelper.NormalizeName(packet.Name);

        if (!GuildHelper.IsValidName(guildName))
        {
            connection.SendTunneled(new GuildPaidRenameCheckReplyPacket
            {
                Guid = guildGuid,
                Name = guildName,
                Result = 2
            });

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var role = GuildHelper.GetMemberRole(dbContext, guildGuid, connection.Player.Guid);

        _logger.LogTrace(
            "Guild rename request. PacketGuildGuid: {packetGuildGuid}, GuildGuid: {guildGuid}, CharacterId: {characterId}, Role: {role}, Name: \"{name}\"",
            packet.Guid,
            guildGuid,
            characterId,
            role,
            guildName);

        if (!GuildHelper.IsLeaderRole(role))
        {
            connection.SendTunneled(new GuildPaidRenameCheckReplyPacket
            {
                Guid = guildGuid,
                Name = guildName,
                Result = 1
            });

            return true;
        }

        if (GuildHelper.IsNameTaken(dbContext, guildGuid, guildName))
        {
            connection.SendTunneled(new GuildPaidRenameCheckReplyPacket
            {
                Guid = guildGuid,
                Name = guildName,
                Result = 3
            });

            return true;
        }

        if (!GuildHelper.ApplyRename(_zoneManager, dbContext, connection, guildGuid, guildName, out var notifiedPlayers))
            return true;

        _logger.LogTrace(
            "Guild rename broadcast. GuildGuid: {guildGuid}, Name: \"{name}\", NotifiedPlayers: {notifiedPlayers}",
            guildGuid,
            guildName,
            notifiedPlayers);

        connection.SendTunneled(new GuildPaidRenameCheckReplyPacket
        {
            Guid = guildGuid,
            Name = guildName,
            Result = 5
        });

        return true;
    }

}
