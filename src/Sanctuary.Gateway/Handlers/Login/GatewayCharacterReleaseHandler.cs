using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GatewayCharacterReleaseHandler
{
    private static ILogger _logger = null!;
    private static GatewayServer _gatewayServer = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(GatewayCharacterReleaseHandler));

        _gatewayServer = serviceProvider.GetRequiredService<GatewayServer>();
    }

    public static bool HandlePacket(LoginConnection connection, Span<byte> data)
    {
        if (!GatewayCharacterReleaseRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(GatewayCharacterReleaseRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(GatewayCharacterReleaseRequest), packet);

        connection.Send(new GatewayCharacterReleaseReply
        {
            RequestId = packet.RequestId,
            Released = _gatewayServer.TryReleaseIdleCharacter(packet.Id, packet.IdleThresholdMilliseconds)
        });

        return true;
    }
}
