using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketPickupAllFixturesHandler
{
    public const short OpCode = 4;

    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketPickupAllFixturesHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!TryDeserialize(data))
        {
            _logger.LogError("Failed to deserialize ClientHousingPacketPickupAllFixtures.");
            return false;
        }

        if (connection.Player.Zone is HousingZone zone)
            zone.Runtime.PickupAllFixtures(connection.Player);

        return true;
    }

    private static bool TryDeserialize(ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);

        if (!reader.TryRead(out short baseOpCode) || baseOpCode != BaseHousingPacket.OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != OpCode)
            return false;

        return reader.RemainingLength == 0;
    }
}
