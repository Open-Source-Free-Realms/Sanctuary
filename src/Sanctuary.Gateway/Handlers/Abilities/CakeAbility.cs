using System;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

using static Sanctuary.Gateway.Handlers.AbilityPacketClientRequestStartAbilityHandler;

namespace Sanctuary.Gateway.Handlers.Abilities;

// Split out of the old handler's big if-chain - same logic, just its own class now.
public sealed class CakeAbility : IConsumableAbility
{
    public bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.Cakes.ContainsKey(itemDefinition.Id);

    public bool Handle(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        _resourceManager.Consumables.Cakes.TryGetValue(itemDefinition.Id, out var cakeDefinition);

        if (IsOnCooldown(connection.Player.Guid, itemDefinition.Id))
            return SendFailure(connection);

        SpawnCakeNpc(connection, cakeDefinition!);

        StartCooldown(connection.Player.Guid, itemDefinition.Id, cakeDefinition!.CooldownMs);
        connection.Player.StartActionBarCooldown(2, slot, itemDefinition.Icon.Id, itemDefinition.NameId, clientItem.Count, cakeDefinition.CooldownMs);

        return true;
    }

    private static void SpawnCakeNpc(GatewayConnection connection, CakeItemDefinition cakeDefinition)
    {
        if (connection.Player.Zone is not StartingZone startingZone)
            return;

        if (!startingZone.TryCreateNpc(out var cakeNpc))
            return;

        cakeNpc.NameId = cakeDefinition.NameId;
        cakeNpc.ModelId = cakeDefinition.ModelId;
        cakeNpc.TextureAlias = "";
        cakeNpc.TintAlias = "";
        cakeNpc.Scale = 1.0f;
        cakeNpc.Animation = cakeDefinition.Animation;
        cakeNpc.HideNamePlate = false;
        cakeNpc.IsInteractable = true;
        cakeNpc.CursorId = (byte)cakeDefinition.CursorId;

        var forwardDirection = Vector3.Transform(new Vector3(0, 0, 1), connection.Player.Rotation);
        var spawnPosition = new Vector4(
            connection.Player.Position.X + forwardDirection.X * 1.5f,
            connection.Player.Position.Y + forwardDirection.Y * 1.5f,
            connection.Player.Position.Z + forwardDirection.Z * 1.5f,
            connection.Player.Position.W
        );

        cakeNpc.Visible = true;
        cakeNpc.UpdatePosition(spawnPosition, connection.Player.Rotation);

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

        var poofEffect = new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = cakeNpc.Guid,
            CompositeEffectId = cakeDefinition.SpawnPoofEffectId,
            Position = spawnPosition,
            Clear = false
        };

        connection.Player.SendTunneled(poofEffect);
        connection.Player.OnAddVisibleNpcs([cakeNpc]);

        foreach (var player in connection.Player.VisiblePlayers.Values)
        {
            player.SendTunneled(poofEffect);
            player.OnAddVisibleNpcs([cakeNpc]);
        }

        var despawnTime = DateTimeOffset.UtcNow.AddMilliseconds(cakeDefinition.LifetimeMs);

        cakeNpc.UpdateEverySecondAction = () =>
        {
            if (DateTimeOffset.UtcNow >= despawnTime)
                DespawnNpc(cakeNpc, cakeDefinition.SpawnPoofEffectId);
        };
    }
}
