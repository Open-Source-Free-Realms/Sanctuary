using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketToggleFloraAllowedHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketToggleFloraAllowedHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketToggleFloraAllowed.TryDeserialize(data, out var clientHousingPacketToggleFloraAllowed))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketToggleFloraAllowed));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketToggleFloraAllowed), clientHousingPacketToggleFloraAllowed);

        if (connection.Player.Zone is not HousingZone housingZone || !housingZone.IsOwner(connection.Player))
            return true;

        housingZone.SetFloraAllowed(!housingZone.IsFloraAllowed);

        return true;
    }
}