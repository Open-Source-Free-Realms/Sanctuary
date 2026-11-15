using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketLockHouseHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketLockHouseHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketLockHouse.TryDeserialize(data, out var clientHousingPacketLockHouse))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketLockHouse));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ClientHousingPacketLockHouse), clientHousingPacketLockHouse);

        if (!HousingHelper.TryGetOwnedZone(connection.Player, out var housingZone))
            return true;

        housingZone.SetLocked(clientHousingPacketLockHouse.IsLocked);

        return true;
    }
}