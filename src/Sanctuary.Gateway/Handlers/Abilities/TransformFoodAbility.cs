using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Handlers.Abilities;

// Transform foods (dog/cat/bat treats, etc). Random ones (Jack-O-Lantern) roll a transform in Handle
// rather than Matches, so it's a fresh roll every use - same as before the move.
public sealed class TransformFoodAbility : ConsumableAbility
{
    public override bool IsInCollection(ClientItemDefinition itemDefinition)
    {
        if (_resourceManager.Consumables.RandomTransformFoods.TryGetValue(itemDefinition.Id, out var randomFood) && randomFood.TransformAbilityIds.Length > 0)
            return true;

        return _resourceManager.Consumables.Transformations.ContainsKey(itemDefinition.ActivatableAbilityId);
    }

    public override bool HandleAbility(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        var transformAbilityId = itemDefinition.ActivatableAbilityId;

        if (_resourceManager.Consumables.RandomTransformFoods.TryGetValue(itemDefinition.Id, out var randomFood) && randomFood.TransformAbilityIds.Length > 0)
            transformAbilityId = randomFood.TransformAbilityIds[System.Random.Shared.Next(randomFood.TransformAbilityIds.Length)];

        _resourceManager.Consumables.Transformations.TryGetValue(transformAbilityId, out var transform);

        if (IsOnCooldown(connection.Player.Guid, itemDefinition.Id))
            return SendFailure(connection);

        if (connection.Player.TemporaryAppearance != 0)
            return SendFailure(connection);

        connection.Player.ApplyTemporaryAppearance(transform!.ModelId, transform.DurationMs, transform.CompositeEffectId);

        StartCooldown(connection.Player.Guid, itemDefinition.Id, transform.CooldownMs);

        var count = clientItem.Count;

        if (itemDefinition.SingleUse)
            ConsumeItem(connection, clientItem, itemDefinition, slot);

        if (count > 1)
            connection.Player.StartActionBarCooldown(ActionBarId, slot, itemDefinition.Icon.Id, itemDefinition.NameId, count - 1, transform.CooldownMs);

        return true;
    }
}
