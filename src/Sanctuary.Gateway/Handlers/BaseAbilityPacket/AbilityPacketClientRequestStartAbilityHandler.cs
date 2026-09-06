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
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    // Tried in order; first match handles it. The default matches anything, so it goes last.
    private static ConsumableAbility[] _consumableAbilities = [];

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger(nameof(AbilityPacketClientRequestStartAbilityHandler));
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();

        var abilityServices = new AbilityServices(
            _logger,
            _resourceManager,
            serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>());

        _consumableAbilities =
        [
            new BoomboxAbility(abilityServices),
            new CakeAbility(abilityServices),
            new SillyStringAbility(abilityServices),
            new TransformFoodAbility(abilityServices),
            new FoodEffectAbility(abilityServices),
            new DefaultConsumableAbility(abilityServices),
        ];
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!AbilityPacketClientRequestStartAbility.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(AbilityPacketClientRequestStartAbility));
            return false;
        }

        if (packet.Data.Id == ConsumableAbility.ActionBarId)
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

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var itemDefinition) ||
            itemDefinition.ActivatableAbilityId == 0)
            return ConsumableAbility.SendFailure(connection);

        foreach (var ability in _consumableAbilities)
        {
            if (ability.Matches(itemDefinition))
                return ability.HandleAbility(connection, packet, packet.Data.Slot, clientItem, itemDefinition);
        }

        return ConsumableAbility.SendFailure(connection);
    }
}
