using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketEnterRequestHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketEnterRequestHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketEnterRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketEnterRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketEnterRequest), packet);

        // TEST
        /* var packetClientBeginZoning = new PacketClientBeginZoning
        {
            Name = "hsg_emptylot_seaside_beach_01",
            Type = 2,
            Position = new(440.632f, -0.071f, 432.801f, 1.0f),
            Rotation = new(-0.9999741f, 0.0f, -0.0072035603f, 0.0f),
            Sky = "sky_seaside24.xml",
            Unknown = 1,
            Id = 1,
            GeometryId = 214,
            OverrideUpdateRadius = true
        }; */

        /* var packetClientBeginZoning = new PacketClientBeginZoning
        {
            Name = "hsg_emptylot_seaside_cliffs_01",
            Type = 2,
            Position = new(568.8f, 50.8f, 517.9f, 1.0f),
            Rotation = new(-1.0f, 0.0f, 0.0f, 0.0f),
            Sky = "sky_seaside24.xml",
            Unknown = 1,
            Id = 1,
            GeometryId = 214,
            Unknown5 = true
        }; */

        /* connection.SendTunneled(packetClientBeginZoning);

        connection.Player.Zone.RemoveEntity(connection.Player);

        var test = _zoneManager.Get(packetClientBeginZoning.Id);

        if (test is null)
            throw new ArgumentNullException(nameof(test));

        connection.Player.Zone = test;

        test.AddPlayer(connection.Player); */

        return true;
    }
}