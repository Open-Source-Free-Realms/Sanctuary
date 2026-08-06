using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GuildNameRequestPacketHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GuildNameRequestPacketHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!GuildNameRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GuildNameRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GuildNameRequestPacket), packet);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var guildName = dbContext.Guilds
            .Where(x => x.Id == packet.Guid)
            .Select(x => x.Name)
            .SingleOrDefault();

        if (string.IsNullOrEmpty(guildName))
            return true;

        var guildNameUpdatePacket = new GuildNameUpdatePacket
        {
            Guid = packet.Guid,
            Name = guildName
        };

        connection.SendTunneled(guildNameUpdatePacket);

        return true;
    }
}