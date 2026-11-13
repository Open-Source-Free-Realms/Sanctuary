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

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GatewayCharacterLogoutHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!GatewayCharacterLogout.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GatewayCharacterLogout));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GatewayCharacterLogout), packet);

        if (!connection.TryLogout(packet.id, packet.Session))
            _logger.LogInformation("Ignored logout for character {id}. A newer login replaced that session.", packet.id);

        return true;
    }
}