using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketGameTimeSyncHandler
{
    private static ILogger _logger = null!;

    private const int ServerRate = 8;
    private const long GameTimeEpoch = 1221361670;

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

        packet.Time = ServerRate * (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - GameTimeEpoch);
        packet.ServerRate = ServerRate;
        packet.UseClientTime = false;

        connection.SendTunneled(packet);

        return true;
    }
}