using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketClientIsReadyHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger(nameof(PacketClientIsReadyHandler));
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        _logger.LogTrace("Received {name} packet.", nameof(PacketClientIsReady));

        connection.Player.Zone.OnClientIsReady(connection.Player);

        return true;
    }
}