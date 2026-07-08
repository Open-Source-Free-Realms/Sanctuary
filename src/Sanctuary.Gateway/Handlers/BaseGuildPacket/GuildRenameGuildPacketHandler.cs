using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
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
        var guildName = NormalizeGuildName(packet.Name);

        if (!IsValidGuildName(guildName))
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
        var guildMember = dbContext.GuildMembers
            .AsNoTracking()
            .SingleOrDefault(x => x.GuildId == guildGuid && x.Id == characterId);

        _logger.LogTrace(
            "Guild rename request. PacketGuildGuid: {packetGuildGuid}, GuildGuid: {guildGuid}, CharacterId: {characterId}, Role: {role}, Name: \"{name}\"",
            packet.Guid,
            guildGuid,
            characterId,
            guildMember?.Role,
            guildName);

        if (guildMember?.Role != 1)
        {
            connection.SendTunneled(new GuildPaidRenameCheckReplyPacket
            {
                Guid = guildGuid,
                Name = guildName,
                Result = 1
            });

            return true;
        }

        var normalizedGuildName = guildName.ToLower();
        var nameTaken = dbContext.Guilds.Any(x => x.Id != guildGuid && x.Name.ToLower() == normalizedGuildName);

        if (nameTaken)
        {
            connection.SendTunneled(new GuildPaidRenameCheckReplyPacket
            {
                Guid = guildGuid,
                Name = guildName,
                Result = 3
            });

            return true;
        }

        var updated = dbContext.Guilds
            .Where(x => x.Id == guildGuid)
            .ExecuteUpdate(x => x.SetProperty(g => g.Name, guildName));

        if (updated <= 0)
            return true;

        connection.Player.GuildData.Name = guildName;

        var guildNameUpdatePacket = new GuildNameUpdatePacket
        {
            Guid = guildGuid,
            Name = guildName
        };

        var notifiedPlayers = 0;
        foreach (var player in _zoneManager.GetPlayers())
        {
            if (player.GuildData is null || player.GuildData.Guid != guildGuid)
                continue;

            player.GuildData.Name = guildName;
            player.SendTunneled(guildNameUpdatePacket);
            notifiedPlayers++;
        }

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

    private static string NormalizeGuildName(string? name)
    {
        return string.Join(' ', (name ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsValidGuildName(string name)
    {
        if (name.Length is < 3 or > 32)
            return false;

        return name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '\'' or '-');
    }
}
