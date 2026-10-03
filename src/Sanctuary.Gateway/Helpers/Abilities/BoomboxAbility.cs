using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Helpers.Abilities;

public sealed class BoomboxAbility(AbilityServices services) : ConsumableAbility(services)
{
    public override bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.Boomboxes.ContainsKey(itemDefinition.Id);

    public override bool HandleAbility(Player player, AbilityPacketClientRequestStartAbility abilityPacketClientRequestStartAbility, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        if (player.IsItemOnCooldown(itemDefinition.Id))
            return SendFailure(player);

        if (!_resourceManager.Consumables.Boomboxes.TryGetValue(itemDefinition.Id, out var definition))
            return SendFailure(player);

        SpawnBoomboxNpc(player, itemDefinition, definition);

        player.StartItemCooldown(itemDefinition.Id, ClampCooldown(definition.DurationMs));
        player.StartActionBarCooldown(ActionBarId, slot, itemDefinition.Icon.Id, itemDefinition.NameId, clientItem.Count, ClampCooldown(definition.DurationMs));

        return true;
    }

    private void SpawnBoomboxNpc(Player player, ClientItemDefinition itemDefinition, BoomboxDefinition definition)
    {
        var modelId = definition.ModelId;
        var effectIds = definition.EffectIds;
        var effectId = effectIds.Length > 0 ? effectIds[System.Random.Shared.Next(effectIds.Length)] : 0;

        var transformModelId = definition.TransformModelId;

        var leftDirection = Vector3.Transform(new Vector3(-1, 0, 0), player.Rotation);
        var spawnPosition = new Vector4(
            player.Position.X + leftDirection.X * definition.SpawnOffset,
            player.Position.Y + leftDirection.Y * definition.SpawnOffset,
            player.Position.Z + leftDirection.Z * definition.SpawnOffset,
            player.Position.W
        );

        var boomboxNpc = SpawnNpc(player, spawnPosition, npc =>
        {
            npc.NameId = 0;
            npc.ModelId = modelId;
            npc.Name = "Boombox";
            npc.TextureAlias = itemDefinition.TextureAlias ?? "";
            npc.TintAlias = itemDefinition.TintAlias ?? "";
            npc.Scale = 1.0f;
            npc.Animation = definition.SpawnAnimationId; // Bouncing animation
            npc.CompositeEffectId = effectId; // Owned by the entity, so the client stops it on RemovePlayer
            npc.HideNamePlate = true;
            npc.IsInteractable = false;
        });

        if (boomboxNpc is null)
            return;

        var poofRecipients = BroadcastSpawn(player, boomboxNpc, spawnPosition, definition.SpawnEffectId);

        // Tag-attached so it can be stopped cleanly on despawn.
        var songTagId = 0;

        if (effectId != 0)
        {
            songTagId = NextEffectTagId();

            var playerUpdatePacketAddEffectTagCompositeEffect = new PlayerUpdatePacketAddEffectTagCompositeEffect
            {
                Guid = boomboxNpc.Guid,
                TagId = songTagId,
                CompositeEffectId = effectId,
                SourceGuid = boomboxNpc.Guid,
            };

            foreach (var recipient in poofRecipients)
                recipient.SendTunneled(playerUpdatePacketAddEffectTagCompositeEffect);
        }

        BoomboxHelper.StartDanceLoop(player.Zone, boomboxNpc, spawnPosition, definition, songTagId, effectId, transformModelId, itemDefinition.NameId);
    }

}
