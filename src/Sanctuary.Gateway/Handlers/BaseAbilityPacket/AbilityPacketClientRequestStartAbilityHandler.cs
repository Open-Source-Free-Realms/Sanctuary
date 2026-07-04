using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Gateway.Fishing;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class AbilityPacketClientRequestStartAbilityHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(AbilityPacketClientRequestStartAbilityHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!AbilityPacketClientRequestStartAbility.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(AbilityPacketClientRequestStartAbility));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(AbilityPacketClientRequestStartAbility), packet);

        var hasSession = FishingSessions.TryGet(connection.Player.Guid, out var session);

        // The client's use-ability request only carries the action-bar slot (Data.Slot), not the item, so
        // resolve the item from the assignments we tracked (InventoryPacketItemActionBarAssign). Then, if
        // it's a fishing lure and the player is fishing, activate it (+10% to its fish / +treasure for the
        // Treasure Magnet), consume one from the bag, and finish — do NOT send the generic "can't use that"
        // failure below. Any other ability still falls through to the failure (nothing else is implemented).
        var lureDefinitionId = 0;
        if (connection.Player.ActionBarAssignments.TryGetValue(packet.Data.Slot, out var itemInstanceId))
        {
            var clientItem = connection.Player.Items.FirstOrDefault(x => x.Id == itemInstanceId);
            if (clientItem is not null)
                lureDefinitionId = FishingSession.LureItemIdForAbility(clientItem.Definition);
        }

        _logger.LogInformation(
            "Ability request: Data.Id={id} Data.Slot={slot} -> item {item} (lure def {lure}) inFishingSession={session}",
            packet.Data.Id, packet.Data.Slot, itemInstanceId, lureDefinitionId, hasSession);

        if (lureDefinitionId != 0 && hasSession)
        {
            session.SetActiveLure(lureDefinitionId);
            FishingSessions.ConsumeItem(connection.Player, lureDefinitionId);

            _logger.LogInformation("Player {guid} activated fishing lure (def {lure})",
                connection.Player.Guid, lureDefinitionId);

            return true;
        }

        var abilityPacketFailed = new AbilityPacketFailed
        {
            // You can't use that ability right now.
            StringId = 3079
        };

        connection.SendTunneled(abilityPacketFailed);

        return true;
    }
}