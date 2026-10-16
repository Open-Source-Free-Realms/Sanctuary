using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketToggleLockedHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketToggleLockedHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketToggleLocked.TryDeserialize(data, out var clientHousingPacketToggleLocked))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketToggleLocked));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketToggleLocked), clientHousingPacketToggleLocked);

        if (connection.Player.Zone is not HousingZone housingZone || !housingZone.IsOwner(connection.Player))
            return true;

        housingZone.SetLocked(!housingZone.IsLocked);

        return true;
    }
}