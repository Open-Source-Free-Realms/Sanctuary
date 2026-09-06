using System;
using System.Numerics;

using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Handlers.Abilities;

public sealed class CakeAbility(AbilityServices services) : ConsumableAbility(services)
{
    public override bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.Cakes.ContainsKey(itemDefinition.Id);

    public override bool HandleAbility(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        _resourceManager.Consumables.Cakes.TryGetValue(itemDefinition.Id, out var cakeDefinition);

        if (connection.Player.IsItemOnCooldown(itemDefinition.Id))
            return SendFailure(connection);

        SpawnCakeNpc(connection, cakeDefinition!);

        connection.Player.StartItemCooldown(itemDefinition.Id, cakeDefinition!.CooldownMs);
        connection.Player.StartActionBarCooldown(ActionBarId, slot, itemDefinition.Icon.Id, itemDefinition.NameId, clientItem.Count, cakeDefinition.CooldownMs);

        return true;
    }

    private void SpawnCakeNpc(GatewayConnection connection, CakeItemDefinition cakeDefinition)
    {
        var forwardDirection = Vector3.Transform(new Vector3(0, 0, 1), connection.Player.Rotation);
        var spawnPosition = new Vector4(
            connection.Player.Position.X + forwardDirection.X * 1.5f,
            connection.Player.Position.Y + forwardDirection.Y * 1.5f,
            connection.Player.Position.Z + forwardDirection.Z * 1.5f,
            connection.Player.Position.W
        );

        var cakeNpc = SpawnNpc(connection, spawnPosition, npc =>
        {
            npc.NameId = cakeDefinition.NameId;
            npc.ModelId = cakeDefinition.ModelId;
            npc.TextureAlias = "";
            npc.TintAlias = "";
            npc.Scale = 1.0f;
            npc.Animation = cakeDefinition.Animation;
            npc.HideNamePlate = false;
            npc.IsInteractable = true;
            npc.CursorId = (byte)cakeDefinition.CursorId;
        });

        if (cakeNpc is null)
            return;

        if (cakeDefinition.Type == CakeItemType.BossCake)
        {
            cakeNpc.InteractAction = player =>
            {
                var abilityId = cakeDefinition.TransformAbilityIds[Random.Shared.Next(cakeDefinition.TransformAbilityIds.Length)];

                if (_resourceManager.Consumables.Transformations.TryGetValue(abilityId, out var transform))
                    player.ApplyTemporaryAppearance(transform.ModelId, transform.DurationMs, transform.CompositeEffectId);
            };
        }
        else
        {
            var scareReadyTime = DateTimeOffset.MinValue;

            cakeNpc.InteractAction = player =>
            {
                if (DateTimeOffset.UtcNow < scareReadyTime)
                    return;

                scareReadyTime = DateTimeOffset.UtcNow.AddMilliseconds(cakeDefinition.ScareCooldownMs);

                // Every scare group and transform is equally likely.
                var roll = Random.Shared.Next(cakeDefinition.ScareGroups.Length + cakeDefinition.TransformAbilityIds.Length);

                if (roll < cakeDefinition.ScareGroups.Length)
                {
                    foreach (var effectId in cakeDefinition.ScareGroups[roll])
                    {
                        player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect
                        {
                            Guid = cakeNpc.Guid,
                            CompositeEffectId = effectId,
                            Position = cakeNpc.Position,
                            Clear = true
                        }, true);
                    }
                }
                else
                {
                    var abilityId = cakeDefinition.TransformAbilityIds[roll - cakeDefinition.ScareGroups.Length];

                    if (_resourceManager.Consumables.Transformations.TryGetValue(abilityId, out var transform))
                        player.ApplyTemporaryAppearance(transform.ModelId, transform.DurationMs, transform.CompositeEffectId);
                }
            };
        }

        BroadcastSpawn(connection, cakeNpc, spawnPosition, cakeDefinition.SpawnPoofEffectId);

        var despawnTime = DateTimeOffset.UtcNow.AddMilliseconds(cakeDefinition.LifetimeMs);

        cakeNpc.UpdateEverySecondAction = () =>
        {
            if (DateTimeOffset.UtcNow >= despawnTime)
                DespawnNpc(cakeNpc, cakeDefinition.SpawnPoofEffectId);
        };
    }
}
