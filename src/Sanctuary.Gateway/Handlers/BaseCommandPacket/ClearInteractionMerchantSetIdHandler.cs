using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClearInteractionMerchantSetIdHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClearInteractionMerchantSetIdHandler));
    }

    // ClearInteractionMerchantSetId (26/43) has no payload - the client sends it (twice) when
    // the merchant window closes. The reference server sends nothing in reply, so we simply
    // consume it (returning true) to silence the double unhandled-packet warning. The on-screen
    // interaction menu is dismissed by the client itself once the interaction list is sent with
    // the correct managed-session flags (see Npc.OnInteract / Player.OnInteract).
    public static bool HandlePacket(GatewayConnection connection)
    {
        _logger.LogTrace("Received ClearInteractionMerchantSetId (merchant window closed).");

        return true;
    }
}
