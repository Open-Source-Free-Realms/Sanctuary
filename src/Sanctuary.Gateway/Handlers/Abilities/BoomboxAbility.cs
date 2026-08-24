using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Gateway.Handlers.Abilities;

// Split out of the old handler's big if-chain - same logic, just its own class now.
public sealed class BoomboxAbility : ConsumableAbility
{
    // How long a boombox stays out, which is also its use cooldown.
    private const int BoomboxDurationMs = 180_000;

    // PFX_smoke_black_explosion
    private const int PoofEffectId = 21;

    public override bool IsInCollection(ClientItemDefinition itemDefinition) =>
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

        // SpawnNpc already checked this - re-derive the zone reference for StartDanceLoop below.
        if (boomboxNpc is null || connection.Player.Zone is not StartingZone startingZone)
            return;

        var poofRecipients = BroadcastSpawn(connection, boomboxNpc, spawnPosition, PoofEffectId);

        // Tag-attach the song so it plays right away and we can stop it cleanly on despawn.
        var songTagId = 0;

        if (effectId != 0)
        {
            songTagId = System.Threading.Interlocked.Increment(ref _castFxTagCounter);

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

    // Boombox's spawn broadcast doesn't follow the default (Cake-style) strategy: instead of
    // explicitly pushing OnAddVisibleNpcs, it trusts the zone tile system (already populated by
    // SpawnNpc's UpdatePosition call) and only patches the spawner as an edge case if they're
    // outside their own tile range.
    protected override List<Player> BroadcastSpawn(GatewayConnection connection, Npc npc, Vector4 position, int poofEffectId)
    {
        var poofEffect = new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = npc.Guid,
            CompositeEffectId = poofEffectId,
            Position = position,
            Clear = false
        };

        var poofRecipients = npc.VisiblePlayers.Values.ToList();

        if (!npc.VisiblePlayers.ContainsKey(connection.Player.Guid))
        {
            // Spawner is outside zone tile range, send the packets manually.
            connection.Player.SendTunneled(npc.GetAddNpcPacket());
            poofRecipients.Insert(0, connection.Player);
        }

        foreach (var player in poofRecipients)
            player.SendTunneled(poofEffect);

        return poofRecipients;
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

            // Rotate to the next dance when due. Only flag a change when the id actually
            // differs, so multi-dance boomboxes don't restart the crowd every rotation.
            var animChanged = false;

            if (sinceSwitch >= SwitchMs)
            {
                var selected = danceSequence.Length > 0 ? danceSequence[sequenceIndex % danceSequence.Length] : 3501;
                sequenceIndex++;
                sinceSwitch = 0;

                // A single-clip sequence (Totem, Realms Roll) never "changes" id, but the client
                // doesn't loop it forever on its own - it needs a fresh trigger every rotation or it
                // just stops after one play-through.
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

            // On a rotation, re-sync the whole crowd so it stays phase-locked. Otherwise just
            // start late arrivals on the current dance without hitching everyone else.
            if (animChanged)
                SyncDance(inRange, currentAnim);
            else if (newcomers.Count > 0)
                SyncDance(newcomers, currentAnim);

            // Newcomers need the song re-sent too, same as the dance sync above. Unlike the dance sync
            // (targets the PLAYER's own guid, always known client-side), this packet targets the
            // boombox NPC's guid - if a teleported player's zone-tile visibility for that NPC hasn't
            // caught up yet, the client silently drops it as an unknown entity and never plays the
            // song. Guard it the same way the spawn-time send already does: make sure they actually
            // have AddNpc for the boombox before attaching an effect to it.
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
