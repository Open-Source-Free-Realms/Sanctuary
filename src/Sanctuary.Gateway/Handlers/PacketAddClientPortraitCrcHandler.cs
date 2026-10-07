using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketAddClientPortraitCrcHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketAddClientPortraitCrcHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketAddClientPortraitCrc.TryDeserialize(data, out var packetAddClientPortraitCrc))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketAddClientPortraitCrc));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketAddClientPortraitCrc), packetAddClientPortraitCrc);

        connection.Player.UpdatePortrait(packetAddClientPortraitCrc.PortraitCrc);
        return true;
    }
}
