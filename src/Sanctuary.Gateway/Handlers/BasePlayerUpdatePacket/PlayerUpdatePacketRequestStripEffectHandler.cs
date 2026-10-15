using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PlayerUpdatePacketRequestStripEffectHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PlayerUpdatePacketRequestStripEffectHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PlayerUpdatePacketRequestStripEffect.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}. ( Data: {data} )", nameof(PlayerUpdatePacketRequestStripEffect), Convert.ToHexString(data));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PlayerUpdatePacketRequestStripEffect), packet);

        connection.Player.RemoveEffect(packet.TagId);

        return true;
    }
}
