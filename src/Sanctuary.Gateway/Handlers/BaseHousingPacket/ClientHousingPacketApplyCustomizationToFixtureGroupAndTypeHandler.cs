using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketApplyCustomizationToFixtureGroupAndTypeHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketApplyCustomizationToFixtureGroupAndTypeHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketApplyCustomizationToFixtureGroupAndType.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketApplyCustomizationToFixtureGroupAndType));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketApplyCustomizationToFixtureGroupAndType), packet);

        if (connection.Player.Zone is HousingZone zone)
        {
            zone.Runtime.ApplyCustomization(
                connection.Player,
                packet.ItemDefinitionId,
                packet.FixtureGroup,
                packet.FixtureType);
        }

        return true;
    }
}
