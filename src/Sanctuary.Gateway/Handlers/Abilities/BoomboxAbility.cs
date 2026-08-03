using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

using static Sanctuary.Gateway.Handlers.AbilityPacketClientRequestStartAbilityHandler;

namespace Sanctuary.Gateway.Handlers.Abilities;

// Split out of the old handler's big if-chain - same logic, just its own class now.
public sealed class BoomboxAbility : IConsumableAbility
{
    // How long a boombox stays out, which is also its use cooldown.
    private const int BoomboxDurationMs = 180_000;

    public bool Matches(ClientItemDefinition itemDefinition) =>
        _resourceManager.Consumables.Boomboxes.ContainsKey(itemDefinition.Id);

    public bool Handle(GatewayConnection connection, AbilityPacketClientRequestStartAbility packet, int slot, ClientItem clientItem, ClientItemDefinition itemDefinition)
    {
        if (IsOnCooldown(connection.Player.Guid, itemDefinition.Id))
            return SendFailure(connection);

        SpawnBoomboxNpc(connection, itemDefinition);

        StartCooldown(connection.Player.Guid, itemDefinition.Id, BoomboxDurationMs);
        connection.Player.StartActionBarCooldown(2, slot, itemDefinition.Icon.Id, itemDefinition.NameId, clientItem.Count, BoomboxDurationMs);

        return true;
    }

    private static void SpawnBoomboxNpc(GatewayConnection connection, ClientItemDefinition itemDefinition)
    {
        if (connection.Player.Zone is not StartingZone startingZone)
            return;

        if (!startingZone.TryCreateNpc(out var boomboxNpc))
            return;

        _resourceManager.Consumables.Boomboxes.TryGetValue(itemDefinition.Id, out var boomboxDefinition);

        var modelId = boomboxDefinition?.ModelId ?? 1062;
        var effectId = boomboxDefinition?.EffectId ?? 0;
        var danceSequence = boomboxDefinition?.DanceSequence ?? [3501, 3502, 3503, 3504, 3505];

        boomboxNpc.NameId = 0;
        boomboxNpc.ModelId = modelId;
        boomboxNpc.Name = "Boombox";
        boomboxNpc.TextureAlias = itemDefinition.TextureAlias ?? "";
        boomboxNpc.TintAlias = itemDefinition.TintAlias ?? "";
        boomboxNpc.Scale = 1.0f;
        boomboxNpc.Animation = 2100; // Bouncing animation
        boomboxNpc.CompositeEffectId = effectId; // Owned by the entity, so the client stops it on RemovePlayer
        boomboxNpc.HideNamePlate = true;
        boomboxNpc.IsInteractable = false;

        var leftDirection = Vector3.Transform(new Vector3(-1, 0, 0), connection.Player.Rotation);
        var spawnPosition = new Vector4(
            connection.Player.Position.X + leftDirection.X * 2.0f,
            connection.Player.Position.Y + leftDirection.Y * 2.0f,
            connection.Player.Position.Z + leftDirection.Z * 2.0f,
            connection.Player.Position.W
        );

        // Visible must be set before UpdatePosition so the zone tile system sends AddNpc to players in range.
        boomboxNpc.Visible = true;
        boomboxNpc.UpdatePosition(spawnPosition, connection.Player.Rotation);

        var poofEffect = new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = boomboxNpc.Guid,
            CompositeEffectId = 21, // PFX_smoke_black_explosion
            Position = spawnPosition,
            Clear = false
        };

        var poofRecipients = boomboxNpc.VisiblePlayers.Values.ToList();

        if (!boomboxNpc.VisiblePlayers.ContainsKey(connection.Player.Guid))
        {
            // Spawner is outside zone tile range, send the packets manually.
            connection.Player.SendTunneled(boomboxNpc.GetAddNpcPacket());
            poofRecipients.Insert(0, connection.Player);
        }

        foreach (var player in poofRecipients)
            player.SendTunneled(poofEffect);

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

                DespawnNpc(boomboxNpc, 21);
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

            // Newcomers need the song re-sent too, same as the dance sync above.
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
                    player.SendTunneled(songEffect);
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
            AnimationId = BoomboxIdleAnimId,
            Flags = 1
        }, true);
    }
}
