using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// The daily wheel isn't served yet: the request is logged so the next step of the flow can be read from the client.
/// </summary>
[PacketHandler]
public static class CommandPacketInteractionStartWheelHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketInteractionStartWheelHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        _logger.LogInformation("{player} started the daily wheel. ( Data: {data} )", connection.Player.Name.FullName, Convert.ToHexString(data));
        return true;
    }
}
