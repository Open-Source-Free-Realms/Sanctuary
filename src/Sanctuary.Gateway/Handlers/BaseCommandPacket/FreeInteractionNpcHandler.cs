using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FreeInteractionNpcHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(FreeInteractionNpcHandler));
    }

    // The client's "interact with my current selection" click. FreeInteractionNpc (base command
    // opcode 26, sub-opcode 20) carries no payload — the target is whatever the player last
    // selected, which arrived in the preceding CommandPacketSelectPlayer (26/19) and was stored on
    // connection.SelectedGuid. Resolve that entity and open its interaction menu via OnInteract,
    // the same path a direct interact-request uses. OnInteract is a no-op for NPCs that offer no
    // interactions, so ordinary NPCs stay inert while a merchant opens its shop menu.
    // See docs/merchant-shops.md for the full click → menu → shop flow.
    public static bool HandlePacket(GatewayConnection connection)
    {
        if (connection.SelectedGuid == 0)
            return true;

        if (!connection.Player.Zone.TryGetEntity(connection.SelectedGuid, out var entity))
        {
            _logger.LogTrace("FreeInteractionNpc: no entity for selected guid {guid}.", connection.SelectedGuid);
            return true;
        }

        entity.OnInteract(connection.Player);

        return true;
    }
}
