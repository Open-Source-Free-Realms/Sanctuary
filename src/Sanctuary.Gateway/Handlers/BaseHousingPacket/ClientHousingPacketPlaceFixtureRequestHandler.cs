using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketPlaceFixtureRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketPlaceFixtureRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (TryDeserializeCompact(data, out var itemRecordId))
        {
            if (connection.Player.Zone is HousingZone compactZone)
                compactZone.Runtime.BeginPlacement(connection.Player, itemRecordId);

            return true;
        }

        if (!ClientHousingPacketPlaceFixtureRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketPlaceFixtureRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketPlaceFixtureRequest), packet);

        if (connection.Player.Zone is HousingZone zone)
        {
            zone.Runtime.PlaceFixtureRequest(
                connection.Player,
                packet.ItemDefinitionId,
                packet.Position,
                packet.Rotation,
                packet.Scale);
        }

        return true;
    }

    private static bool TryDeserializeCompact(ReadOnlySpan<byte> data, out int itemRecordId)
    {
        itemRecordId = 0;
        var reader = new PacketReader(data);

        if (!reader.TryRead(out short baseOpCode) || baseOpCode != BaseHousingPacket.OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != ClientHousingPacketPlaceFixtureRequest.OpCode)
            return false;

        if (!reader.TryRead(out itemRecordId))
            return false;

        if (!reader.TryRead(out ulong _))
            return false;

        if (!reader.TryRead(out bool _))
            return false;

        return reader.RemainingLength == 0;
    }
}
