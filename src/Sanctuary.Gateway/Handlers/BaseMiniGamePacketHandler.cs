using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// Minigames aren't implemented yet. Every minigame packet the client sends is logged in full, so the request
/// layouts (create, start, cancel, leave, flash payloads) can be confirmed against the client before building on them.
/// </summary>
[PacketHandler]
public static class BaseMiniGamePacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseMiniGamePacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        _logger.LogInformation("{player} sent a minigame packet. ( Data: {data} )",
            connection.Player.Name.FullName, Convert.ToHexString(reader.Span));

        return true;
    }
}
