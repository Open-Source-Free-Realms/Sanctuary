using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Helpers.Abilities;

// Potions, scrolls and buff foods from ItemAbilities.json: heals and energy (instant or over time),
// coin/star boosts and other stat buffs, run speed, size, auras and fireworks.
public sealed class ItemAbility(AbilityServices services) : ConsumableAbility(services)
{
    public override bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.ItemAbilities.ContainsKey(itemDefinition.ActivatableAbilityId);

    public override bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        if (player.IsItemOnCooldown(itemDefinition.Id) ||
            !_resourceManager.ItemAbilities.TryGetValue(itemDefinition.ActivatableAbilityId, out var ability))
            return SendFailure(player);

        var cooldownMs = ClampCooldown(ability.CooldownMs);

        player.StartItemCooldown(itemDefinition.Id, cooldownMs);

        ItemAbilityHelper.Apply(player, ability, itemDefinition.NameId, itemDefinition.Icon.Id);

        FinishActivation(player, clientItem, itemDefinition, slot, cooldownMs, IconTintId(clientItem, itemDefinition.Icon.TintId));

        return true;
    }
}
