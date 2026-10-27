using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketSetEditModeHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketSetEditModeHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketSetEditMode.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketSetEditMode));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketSetEditMode), packet);

        // Leaving edit mode from the client UI also ends GM world placement.
        if (connection.Player.Zone is WorldZone worldZone && worldZone.IsPlacing(connection.Player))
        {
            if (!packet.InEditMode)
                worldZone.EndPlacement(connection.Player);

            return true;
        }

        // No reply outside a house: the client's house info handler assumes a housing instance exists.
        if (connection.Player.Zone is not HousingZone housingZone)
            return true;

        housingZone.SetEditMode(connection.Player, packet.InEditMode);

        return true;
    }
}