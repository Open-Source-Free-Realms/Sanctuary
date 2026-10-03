using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketPortraitDataRequestHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketPortraitDataRequestHandler));
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketPortraitDataRequest.TryDeserialize(data, out var packetPortraitDataRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketPortraitDataRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketPortraitDataRequest), packetPortraitDataRequest);

        if (!_zoneManager.TryGetPlayer(packetPortraitDataRequest.Guid, out var portraitPlayer))
            return true;

        var packetPlayerImageData = portraitPlayer.GetPortraitPacket(packetPortraitDataRequest.Provider);

        if (packetPlayerImageData is null)
            return true;

        connection.SendTunneled(packetPlayerImageData);

        return true;
    }
}