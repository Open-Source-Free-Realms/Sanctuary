using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketInteractionSelectHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;
    private static IInteractionManager _interactionManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketInteractionSelectHandler));

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _interactionManager = serviceProvider.GetRequiredService<IInteractionManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketInteractionSelect.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketInteractionSelect));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CommandPacketInteractionSelect), packet);

        if (!_interactionManager.TryGet(packet.Id, out var interaction))
        {
            _logger.LogError("Invalid interaction. {interaction}", packet.Id);

            return true;
        }

        if (connection.Player.VisiblePlayers.TryGetValue(packet.Guid, out var player))
        {
            interaction.OnInteract(connection.Player, player);
        }
        else if (connection.Player.VisibleNpcs.TryGetValue(packet.Guid, out var npc))
        {
            interaction.OnInteract(connection.Player, npc);
        }
        else
        {
            _logger.LogWarning("Received interaction for unknown entity. {entity}", packet.Guid);

            return true;
        }

        // A choice was made, so dismiss the on-screen interaction menu for that entity. Without
        // this the selected button (e.g. "Merchant") stays stuck on screen — it overlaps the
        // merchant window's close (X), so clicking X re-clicks the button and reopens the shop.
        // Real FR sends count=0 interaction lists; an empty list clears the menu.
        DismissInteractionMenu(connection, packet.Guid);

        // Clear the stored selection now the interaction is resolved. The client bundles a generic
        // FreeInteractionNpc (26/20) "interact with selection" poll into its periodic 141/1 traffic;
        // if SelectedGuid is still the merchant NPC, FreeInteractionNpcHandler re-fires OnInteract on
        // every poll and re-arms the "Merchant" menu — which keeps the interaction live so the
        // merchant window (opened by a REAL NPC click, which sets SelectedGuid via 26/19) never
        // closes and the client floods 141/1. Cleared here (at store-open / 26/10) rather than in the
        // 26/43 close handler because the close bundle delivers 26/20 before 26/43.
        connection.SelectedGuid = 0;

        return true;
    }

    // Sends an empty interaction list for the entity to clear the on-screen interaction menu
    // after a selection. The shop/coin-store window is a separate packet, so this does not
    // close the shop — only the lingering interaction buttons.
    private static void DismissInteractionMenu(GatewayConnection connection, ulong guid)
    {
        var clear = new CommandPacketInteractionList();
        clear.List.Guid = guid;
        connection.Player.SendTunneled(clear);
    }
}
