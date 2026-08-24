using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketRemoveCustomizationFromFixtureGroupAndTypeHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketRemoveCustomizationFromFixtureGroupAndTypeHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketRemoveCustomizationFromFixtureGroupAndType.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketRemoveCustomizationFromFixtureGroupAndType));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketRemoveCustomizationFromFixtureGroupAndType), packet);

        if (connection.Player.Zone is HousingZone zone)
        {
            zone.Runtime.RemoveCustomization(
                connection.Player,
                packet.FixtureGroup,
                packet.FixtureType);
        }

        return true;
    }
}
