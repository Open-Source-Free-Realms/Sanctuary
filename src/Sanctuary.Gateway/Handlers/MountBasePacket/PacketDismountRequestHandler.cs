using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketDismountRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketDismountRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        _logger.LogTrace("Received {name} packet.", nameof(PacketDismountRequest));

        connection.Player.Dismount();
        return true;
    }
}
