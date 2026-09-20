using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Helpers.Abilities;

// Transform foods (dog/cat/bat treats). Random ones (Jack-O-Lantern) roll in HandleAbility rather
// than Matches, so every use is a fresh roll.
public sealed class TransformFoodAbility(AbilityServices services) : ConsumableAbility(services)
{
    public override bool Matches(ClientItemDefinition itemDefinition)
    {
        if (_resourceManager.Consumables.RandomTransformFoods.TryGetValue(itemDefinition.Id, out var randomFood) && randomFood.TransformAbilityIds.Length > 0)
            return true;

        return _resourceManager.Consumables.Transformations.ContainsKey(itemDefinition.ActivatableAbilityId);
    }

    public override bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        var transformAbilityId = itemDefinition.ActivatableAbilityId;

        if (_resourceManager.Consumables.RandomTransformFoods.TryGetValue(itemDefinition.Id, out var randomFood) && randomFood.TransformAbilityIds.Length > 0)
        {
            var previousIndex = player.LastRandomTransformIndex(itemDefinition.Id);
            var index = RollExcluding(randomFood.TransformAbilityIds.Length, previousIndex);

            player.SetLastRandomTransformIndex(itemDefinition.Id, index);
            transformAbilityId = randomFood.TransformAbilityIds[index];
        }

        var hasTransform = _resourceManager.Consumables.Transformations.TryGetValue(transformAbilityId, out var transform);
        var cooldownMs = hasTransform ? transform!.CooldownMs
            : _resourceManager.Consumables.FoodEffects.TryGetValue(transformAbilityId, out var foodEffect) ? foodEffect.CooldownMs : 0;

        if (player.IsItemOnCooldown(itemDefinition.Id))
            return SendFailure(player);

        if (hasTransform && player.TemporaryAppearance != 0)
            return SendFailure(player);

        ApplyTransformOrFoodEffect(player, transformAbilityId, itemDefinition.NameId);

        player.StartItemCooldown(itemDefinition.Id, cooldownMs);

        FinishActivation(player, clientItem, itemDefinition, slot, cooldownMs);

        return true;
    }
}
