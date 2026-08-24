using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Gateway.Handlers.Abilities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class AbilityPacketClientRequestStartAbilityHandler
{
    // Tries each ability in order; first match handles it.
    private static readonly ConsumableAbility[] _consumableAbilities =
    [
        new BoomboxAbility(),
        new CakeAbility(),
        new SillyStringAbility(),
        new TransformFoodAbility(),
        new FoodEffectAbility(),
    ];

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        ConsumableAbility._logger = loggerFactory.CreateLogger(nameof(AbilityPacketClientRequestStartAbilityHandler));

        ConsumableAbility._resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        ConsumableAbility._dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!AbilityPacketClientRequestStartAbility.TryDeserialize(data, out var packet))
        {
            ConsumableAbility._logger.LogError("Failed to deserialize {packet}.", nameof(AbilityPacketClientRequestStartAbility));
            return false;
        }

        if (packet.Data.Id == 2)
            return HandleItemAbility(connection, packet);

        return ConsumableAbility.SendFailure(connection);
    }

    private static bool HandleItemAbility(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet)
    {
        connection.Player.ActionBars.TryGetValue(ConsumableAbility.ActionBarId, out var actionBar);

        if (actionBar is null || !actionBar.Slots.TryGetValue(packet.Data.Slot, out var slot) || slot.IsEmpty)
            return ConsumableAbility.SendFailure(connection);

        if (!connection.Player.ActionBarItemGuids.TryGetValue(ConsumableAbility.ActionBarId, out var slotItemGuids) ||
            !slotItemGuids.TryGetValue(packet.Data.Slot, out var itemGuid))
            return ConsumableAbility.SendFailure(connection);

        var clientItem = connection.Player.Items.FirstOrDefault(x => x.Id == itemGuid);

        if (clientItem is null)
            return ConsumableAbility.SendFailure(connection);

        if (!ConsumableAbility._resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var itemDefinition) ||
            itemDefinition.ActivatableAbilityId == 0)
            return ConsumableAbility.SendFailure(connection);

        // Anything unrecognized falls through to the generic case below.
        foreach (var ability in _consumableAbilities)
        {
            if (ability.IsInCollection(itemDefinition))
                return ability.HandleAbility(connection, packet, packet.Data.Slot, clientItem, itemDefinition);
        }

        ConsumableAbility.PlayEffect(connection, itemDefinition.CompositeEffectId);

        if (itemDefinition.SingleUse)
            return ConsumableAbility.ConsumeItem(connection, clientItem, itemDefinition, packet.Data.Slot);

        return true;
    }
}
