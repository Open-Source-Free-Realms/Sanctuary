using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketPlaceFixtureHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketPlaceFixtureHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketPlaceFixture.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketPlaceFixture));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketPlaceFixture), packet);

        if (connection.Player.Zone is WorldZone worldZone && worldZone.IsPlacing(connection.Player))
        {
            worldZone.PlaceFixture(connection.Player, packet);
            return true;
        }

        if (connection.Player.Zone is not HousingZone housingZone)
            return true;

        housingZone.PlaceFixture(connection.Player, packet);

        return true;
    }
}