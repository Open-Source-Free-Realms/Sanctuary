using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class GatewayCharacterLogoutHandler
{
    private static ILogger _logger = null!;
    private static ILogger _serverEvents = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GatewayCharacterLogoutHandler));
        _serverEvents = loggerFactory.CreateLogger("ServerEvents");
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!GatewayCharacterLogout.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GatewayCharacterLogout));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GatewayCharacterLogout), packet);

        connection.OnlineCharacters.Remove(packet.id);

        _serverEvents.LogInformation(
            "Gateway marked character offline. CharacterId: {characterId}, Gateway: {gateway}",
            packet.id,
            connection.ServerAddress);

        return true;
    }
}
