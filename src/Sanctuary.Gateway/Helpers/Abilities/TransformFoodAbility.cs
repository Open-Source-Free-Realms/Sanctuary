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
            transformAbilityId = randomFood.TransformAbilityIds[System.Random.Shared.Next(randomFood.TransformAbilityIds.Length)];

        _resourceManager.Consumables.Transformations.TryGetValue(transformAbilityId, out var transform);

        if (player.IsItemOnCooldown(itemDefinition.Id))
            return SendFailure(player);

        if (player.TemporaryAppearance != 0)
            return SendFailure(player);

        player.ApplyTemporaryAppearance(transform!.ModelId, transform.DurationMs, transform.CompositeEffectId, itemDefinition.NameId);

        player.StartItemCooldown(itemDefinition.Id, transform.CooldownMs);

        FinishActivation(player, clientItem, itemDefinition, slot, transform.CooldownMs);

        return true;
    }
}
