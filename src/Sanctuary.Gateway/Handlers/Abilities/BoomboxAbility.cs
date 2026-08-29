using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Handlers.Abilities;

public sealed class BoomboxAbility(AbilityServices services) : ConsumableAbility(services)
{
    // How long a boombox stays out, which is also its use cooldown.
    private const int BoomboxDurationMs = 180_000;

    // PFX_smoke_black_explosion
    private const int PoofEffectId = 21;

    public override bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.Boomboxes.ContainsKey(itemDefinition.Id);

    public override bool HandleAbility(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        if (IsOnCooldown(connection.Player.Guid, itemDefinition.Id))
            return SendFailure(connection);

        SpawnBoomboxNpc(connection, itemDefinition);

        StartCooldown(connection.Player.Guid, itemDefinition.Id, BoomboxDurationMs);
        connection.Player.StartActionBarCooldown(ActionBarId, slot, itemDefinition.Icon.Id, itemDefinition.NameId, clientItem.Count, BoomboxDurationMs);

        return true;
    }

    private void SpawnBoomboxNpc(GatewayConnection connection, ClientItemDefinition itemDefinition)
    {
        _resourceManager.Consumables.Boomboxes.TryGetValue(itemDefinition.Id, out var boomboxDefinition);

        var modelId = boomboxDefinition?.ModelId ?? 1062;
        var effectId = boomboxDefinition?.EffectId ?? 0;
        var danceSequence = boomboxDefinition?.DanceSequence ?? [3501, 3502, 3503, 3504, 3505];

        var leftDirection = Vector3.Transform(new Vector3(-1, 0, 0), connection.Player.Rotation);
        var spawnPosition = new Vector4(
            connection.Player.Position.X + leftDirection.X * 2.0f,
            connection.Player.Position.Y + leftDirection.Y * 2.0f,
            connection.Player.Position.Z + leftDirection.Z * 2.0f,
            connection.Player.Position.W
        );

        var boomboxNpc = SpawnNpc(connection, spawnPosition, npc =>
        {
            npc.NameId = 0;
            npc.ModelId = modelId;
            npc.Name = "Boombox";
            npc.TextureAlias = itemDefinition.TextureAlias ?? "";
            npc.TintAlias = itemDefinition.TintAlias ?? "";
            npc.Scale = 1.0f;
            npc.Animation = 2100; // Bouncing animation
            npc.CompositeEffectId = effectId; // Owned by the entity, so the client stops it on RemovePlayer
            npc.HideNamePlate = true;
            npc.IsInteractable = false;
        });

        if (boomboxNpc is null || connection.Player.Zone is not StartingZone startingZone)
            return;

        var poofRecipients = BroadcastSpawn(connection, boomboxNpc, spawnPosition, PoofEffectId);

        // Tag-attached so it can be stopped cleanly on despawn.
        var songTagId = 0;

        if (effectId != 0)
        {
            songTagId = NextEffectTagId();

            var songEffect = new PlayerUpdatePacketAddEffectTagCompositeEffect
            {
                Guid = boomboxNpc.Guid,
                TagId = songTagId,
                CompositeEffectId = effectId,
                SourceGuid = boomboxNpc.Guid,
            };

            foreach (var player in poofRecipients)
                player.SendTunneled(songEffect);
        }

        StartDanceLoop(startingZone, boomboxNpc, spawnPosition, danceSequence, songTagId, effectId);
    }

    private static void StartDanceLoop(StartingZone startingZone, Npc boomboxNpc, Vector4 spawnPosition, int[] danceSequence, int songTagId, int effectId)
    {
        const float BoomboxRangeInMeters = 15.0f;
        const int SwitchMs = 4000;

        var danceCenter = new Vector3(spawnPosition.X, spawnPosition.Y, spawnPosition.Z);

        var dancing = new HashSet<ulong>();
        var elapsedMs = 0;
        var sinceSwitch = SwitchMs; // so a dance starts on the first tick
        var sequenceIndex = 0;
        var previousAnim = -1;
        var currentAnim = 0;

        boomboxNpc.UpdateEverySecondAction = () =>
        {
            if (elapsedMs >= BoomboxDurationMs)
            {
                foreach (var player in startingZone.Players.Where(p => dancing.Contains(p.Guid)))
                    StopDancing(player);

                if (songTagId != 0)
                {
                    var stopSong = new PlayerUpdatePacketRemoveEffectTagCompositeEffect
                    {
                        Guid = boomboxNpc.Guid,
                        TagId = songTagId,
                    };

                    foreach (var player in startingZone.Players)
                        player.SendTunneled(stopSong);
                }

                DespawnNpc(boomboxNpc, PoofEffectId);
                return;
            }

            // Only flag a change when the id differs, so multi-dance boomboxes don't restart
            // the crowd every rotation.
            var animChanged = false;

            if (sinceSwitch >= SwitchMs)
            {
                var selected = danceSequence.Length > 0 ? danceSequence[sequenceIndex % danceSequence.Length] : 3501;
                sequenceIndex++;
                sinceSwitch = 0;

                // A single-clip sequence (Totem, Realms Roll) never changes id, and the client
                // stops after one play-through unless it's re-triggered every rotation.
                if (selected != previousAnim || danceSequence.Length <= 1)
                {
                    currentAnim = selected;
                    previousAnim = selected;
                    animChanged = true;
                }
            }

            var players = startingZone.Players.ToList();
            var inRange = players.Where(p =>
                Vector3.Distance(new Vector3(p.Position.X, p.Position.Y, p.Position.Z), danceCenter) <= BoomboxRangeInMeters)
                .ToList();
            var inRangeGuids = inRange.Select(p => p.Guid).ToHashSet();

            foreach (var player in players.Where(p => dancing.Contains(p.Guid) && !inRangeGuids.Contains(p.Guid)))
                StopDancing(player);

            var newcomers = inRange.Where(p => !dancing.Contains(p.Guid)).ToList();
            dancing = inRangeGuids;

            // Re-sync everyone on a rotation to stay phase-locked, otherwise only start late
            // arrivals so the rest don't hitch.
            if (animChanged)
                SyncDance(inRange, currentAnim);
            else if (newcomers.Count > 0)
                SyncDance(newcomers, currentAnim);

            // This targets the boombox's guid, not the player's, so a newcomer whose tile
            // visibility hasn't caught up drops it as an unknown entity and never hears the song.
            // Make sure they have AddNpc first.
            if (songTagId != 0 && newcomers.Count > 0)
            {
                var songEffect = new PlayerUpdatePacketAddEffectTagCompositeEffect
                {
                    Guid = boomboxNpc.Guid,
                    TagId = songTagId,
                    CompositeEffectId = effectId,
                    SourceGuid = boomboxNpc.Guid,
                };

                foreach (var player in newcomers)
                {
                    if (!boomboxNpc.VisiblePlayers.ContainsKey(player.Guid))
                        player.SendTunneled(boomboxNpc.GetAddNpcPacket());

                    player.SendTunneled(songEffect);
                }
            }

            elapsedMs += 1000;
            sinceSwitch += 1000;
        };
    }

    private static void SyncDance(List<Player> targets, int animationId)
    {
        if (targets.Count == 0)
            return;

        var sync = new PlayerUpdatePacketSetSynchronizedAnimations();

        foreach (var player in targets)
            sync.Animations.Add(new PlayerUpdatePacketSetSynchronizedAnimations.Animation { Guid = player.Guid, AnimationId = animationId });

        var recipients = new HashSet<Player>(targets);

        foreach (var player in targets)
            foreach (var visiblePlayer in player.VisiblePlayers.Values)
                recipients.Add(visiblePlayer);

        foreach (var recipient in recipients)
            recipient.SendTunneled(sync);
    }

    private static void StopDancing(Player player)
    {
        player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
        {
            Guid = player.Guid,
            AnimationId = IdleAnimationId,
            Flags = 1
        }, true);
    }
}
