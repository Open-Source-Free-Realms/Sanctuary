using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketMountRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketMountRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketMountRequest.TryDeserialize(data, out var packetMountRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketMountRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketMountRequest), packetMountRequest);

        var mount = connection.Player.Mount;

        if (mount is null || mount.Guid != packetMountRequest.MountGuid || mount.RiderSeated)
            return true;

        PacketMountSpawnHandler.SendMountResponse(connection.Player, mount);

        return true;
    }
}
