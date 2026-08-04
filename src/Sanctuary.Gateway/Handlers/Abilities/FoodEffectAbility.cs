using Sanctuary.Packet;
using Sanctuary.Packet.Common;

using static Sanctuary.Gateway.Handlers.AbilityPacketClientRequestStartAbilityHandler;

namespace Sanctuary.Gateway.Handlers.Abilities;

// Food with just a plain visual/chat effect, nothing fancier. TriggerAbilityEffect stays on the old
// handler since the generic fallback below needs it too.
public sealed class FoodEffectAbility : IConsumableAbility
{
    private const int FoodEffectCooldownMs = 120_000;

    public bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.FoodEffects.ContainsKey(itemDefinition.ActivatableAbilityId);

    public bool Handle(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        if (IsOnCooldown(connection.Player.Guid, itemDefinition.Id))
            return SendFailure(connection);

        StartCooldown(connection.Player.Guid, itemDefinition.Id, FoodEffectCooldownMs);

        TriggerAbilityEffect(connection, itemDefinition);

        var count = clientItem.Count;
        var hasItemLeft = !itemDefinition.SingleUse || count > 1;

        if (itemDefinition.SingleUse)
            ConsumeItem(connection, clientItem, itemDefinition, slot);

        if (hasItemLeft)
            connection.Player.StartActionBarCooldown(2, slot, itemDefinition.Icon.Id, itemDefinition.NameId,
                itemDefinition.SingleUse ? count - 1 : count, FoodEffectCooldownMs);

        return true;
    }
}
