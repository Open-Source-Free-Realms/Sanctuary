using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GuildPaidRenameCheckRequestPacketHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildPaidRenameCheckRequestPacketHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildPaidRenameCheckRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildPaidRenameCheckRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildPaidRenameCheckRequestPacket), packet);

        var guildName = NormalizeGuildName(packet.Name);
        var guildGuid = ResolveGuildGuid(connection, packet.Guid);
        var result = 5;
        int? role = null;

        if (!IsValidGuildName(guildName))
        {
            result = 2;
        }
        else
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
            role = dbContext.GuildMembers
                .AsNoTracking()
                .Where(x => x.GuildId == guildGuid && x.Id == characterId)
                .Select(x => (int?)x.Role)
                .SingleOrDefault();

            if (role != 1)
            {
                result = 1;
            }
            else
            {
                var normalizedGuildName = guildName.ToLower();
                var nameTaken = dbContext.Guilds.Any(x => x.Id != guildGuid && x.Name.ToLower() == normalizedGuildName);

                if (nameTaken)
                    result = 3;
            }
        }

        var guildPaidRenameCheckReplyPacket = new GuildPaidRenameCheckReplyPacket
        {
            Guid = guildGuid,
            Name = guildName,
            Result = result
        };

        _logger.LogTrace(
            "Guild paid rename check result. PacketGuildGuid: {packetGuildGuid}, GuildGuid: {guildGuid}, CharacterId: {characterId}, Role: {role}, Name: \"{name}\", Result: {result}",
            packet.Guid,
            guildGuid,
            GuidHelper.GetPlayerId(connection.Player.Guid),
            role,
            guildName,
            result);

        connection.SendTunneled(guildPaidRenameCheckReplyPacket);

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

    private static ulong ResolveGuildGuid(GatewayConnection connection, ulong packetGuildGuid)
    {
        if (connection.Player.GuildData is not null)
            return connection.Player.GuildData.Guid;

        return packetGuildGuid;
    }
}
