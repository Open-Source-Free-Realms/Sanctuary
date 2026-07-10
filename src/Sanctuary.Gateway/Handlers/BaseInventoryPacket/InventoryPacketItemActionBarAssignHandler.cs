using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class InventoryPacketItemActionBarAssignHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketItemActionBarAssignHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketItemActionBarAssign.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketItemActionBarAssign));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketItemActionBarAssign), packet);

        var clientUpdatePacketUpdateActionBarSlot = new ClientUpdatePacketUpdateActionBarSlot
        {
            Data =
            {
                Id = 2,
                Slot = packet.Slot
            }
        };

        // Ensure action bar exists
        if (!connection.Player.ActionBars.ContainsKey(2))
        {
            connection.Player.ActionBars[2] = new Packet.Common.ClientActionBar { Id = 2 };
        }

        if (packet.Guid == 0)
        {
            clientUpdatePacketUpdateActionBarSlot.Slot.IsEmpty = true;

            // Remove from server-side tracking
            connection.Player.ActionBars[2].Slots.Remove(packet.Slot);

            if (connection.Player.ActionBarItemGuids.ContainsKey(2))
            {
                connection.Player.ActionBarItemGuids[2].Remove(packet.Slot);
            }

            connection.SendTunneled(clientUpdatePacketUpdateActionBarSlot);

            return true;
        }

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == packet.Guid);

        if (clientItem is null)
        {
            _logger.LogWarning("User tried to equip unknown item. {guid}", packet.Guid);
            return true;
        }

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("User tried to equip unknown item definition. {guid} {definition}", packet.Guid, clientItem.Definition);
            return true;
        }

        clientUpdatePacketUpdateActionBarSlot.Slot.IsEmpty = false;

        clientUpdatePacketUpdateActionBarSlot.Slot.IconId = clientItemDefinition.Icon.Id;
        clientUpdatePacketUpdateActionBarSlot.Slot.NameId = clientItemDefinition.NameId;

        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown5 = 1;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown6 = 4;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown7 = 15;

        clientUpdatePacketUpdateActionBarSlot.Slot.Enabled = true;

        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown10 = 1000;
        clientUpdatePacketUpdateActionBarSlot.Slot.TotalRefreshTime = 1000;
        clientUpdatePacketUpdateActionBarSlot.Slot.Quantity = clientItem.Count;
        clientUpdatePacketUpdateActionBarSlot.Slot.ForceDismount = true;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown15 = 1000;

        // Store the slot information server-side with the item GUID
        var slotData = new Packet.Common.ActionBarSlot
        {
            IsEmpty = false,
            IconId = clientItemDefinition.Icon.Id,
            NameId = clientItemDefinition.NameId,
            Unknown5 = 1,
            Unknown6 = 4,
            Unknown7 = 15,
            Enabled = true,
            Unknown10 = 1000,
            TotalRefreshTime = 1000,
            Quantity = clientItem.Count,
            ForceDismount = true,
            Unknown15 = 1000
        };

        connection.Player.ActionBars[2].Slots[packet.Slot] = slotData;

        // Track the item GUID for this slot
        if (!connection.Player.ActionBarItemGuids.ContainsKey(2))
        {
            connection.Player.ActionBarItemGuids[2] = new Dictionary<int, int>();
        }
        connection.Player.ActionBarItemGuids[2][packet.Slot] = packet.Guid;

        connection.SendTunneled(clientUpdatePacketUpdateActionBarSlot);

        return true;
    }
}