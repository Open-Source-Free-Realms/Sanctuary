using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketGameTimeSyncHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketGameTimeSyncHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PacketGameTimeSync.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketGameTimeSync));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketGameTimeSync), packet);

        packet.Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        packet.ServerRate = 8;
        packet.UseClientTime = false;

        connection.SendTunneled(packet);

        return true;
    }
}