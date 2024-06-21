using System;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Packet;
using Sanctuary.Database;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ChangeNameRequestPacketHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ChangeNameRequestPacketHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ChangeNameRequestPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ChangeNameRequestPacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ChangeNameRequestPacket), packet);

        var nameChangeResponsePacket = new NameChangeResponsePacket();

        // TODO: Handle other types
        if (packet.Type != Packet.Common.NameChangeType.Character)
        {
            nameChangeResponsePacket.Result = 2; // ChangeNameResponse.Error

            connection.SendTunneled(nameChangeResponsePacket);

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters.FirstOrDefault(x => x.Guid == packet.Guid);

        if (dbCharacter is null)
        {
            nameChangeResponsePacket.Result = 3; // ChangeNameResponse.Error

            connection.SendTunneled(nameChangeResponsePacket);

            return true;
        }

        dbCharacter.FirstName = packet.Name.FirstName;
        dbCharacter.LastName = packet.Name.LastName;

        if (dbContext.SaveChanges() <= 0)
        {
            nameChangeResponsePacket.Result = 4; // ChangeNameResponse.Error

            connection.SendTunneled(nameChangeResponsePacket);

            return true;
        }

        connection.Player.Name.FirstName = packet.Name.FirstName;
        connection.Player.Name.LastName = packet.Name.LastName;

        nameChangeResponsePacket.Type = packet.Type;
        nameChangeResponsePacket.Guid = packet.Guid;
        nameChangeResponsePacket.Name = packet.Name;

        nameChangeResponsePacket.Result = 1;

        connection.SendTunneled(nameChangeResponsePacket);

        var playerUpdatePacketRenamePlayer = new PlayerUpdatePacketRenamePlayer();

        playerUpdatePacketRenamePlayer.Guid = connection.Player.Guid;
        playerUpdatePacketRenamePlayer.Name = connection.Player.Name;

        connection.Player.SendTunneledToVisible(playerUpdatePacketRenamePlayer, true);

        return true;
    }
}