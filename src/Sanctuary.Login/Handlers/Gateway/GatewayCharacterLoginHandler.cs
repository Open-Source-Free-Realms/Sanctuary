using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class GatewayCharacterLoginHandler
{
    private static ILogger _logger = null!;
    private static ILogger _serverEvents = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GatewayCharacterLoginHandler));
        _serverEvents = loggerFactory.CreateLogger("ServerEvents");
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!GatewayCharacterLogin.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GatewayCharacterLogin));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GatewayCharacterLogin), packet);

        connection.OnlineCharacters.Add(packet.Id);

        _serverEvents.LogInformation(
            "Gateway marked character online. CharacterId: {characterId}, Gateway: {gateway}",
            packet.Id,
            connection.ServerAddress);

        return true;
    }
}
