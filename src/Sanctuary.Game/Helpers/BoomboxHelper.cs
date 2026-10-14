using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

public static class BoomboxHelper
{
    private const int IdleAnimationId = 1;
    private const int RealmsRollItemId = 1180;
    private const int BoomboxRemoveDurationMs = 500;

    private sealed record DanceSource(Vector3 Center, BoomboxDefinition Definition, long ExpiresAt)
    {
        public bool Covers(Vector3 position, long now) => ExpiresAt > now && Vector3.Distance(position, Center) <= Definition.Range;
    }
    private sealed class ZoneDances
    {
        public readonly Dictionary<ulong, DanceSource> Sources = new();
        public readonly BoomboxDanceSelection Selection = new();

        public ulong SelectOwner(Player player, long now)
        {
            var position = player.Position.ToVector3();
            var available = Sources.Where(pair => pair.Value.Covers(position, now)).ToArray();
            return Selection.Select(player.Guid, now,
                available.Select(pair => (pair.Key, pair.Value.Definition.Priority)));
        }
    }
    private static readonly ConditionalWeakTable<IZone, ZoneDances> DanceStates = new();

    public static bool IsInDanceRange(Player player)
    {
        if (player.BoomboxDanceOwner != 0)
            return true;

        if (!DanceStates.TryGetValue(player.Zone, out var zoneDances))
            return false;

        var position = player.Position.ToVector3();

        lock (zoneDances)
        {
            var now = Environment.TickCount64;

            return zoneDances.Sources.Values.Any(source => source.Covers(position, now));
        }
    }
    public static void RemovePlayer(IZone zone, ulong guid)
    {
        if (!DanceStates.TryGetValue(zone, out var zoneDances))
            return;

        lock (zoneDances)
            zoneDances.Selection.Remove(guid);
    }

    public static void StartDanceLoop(IZone zone, Npc boomboxNpc, Vector4 spawnPosition, BoomboxDefinition definition, int songTagId, int effectId, int transformModelId, int transformBuffNameId, bool permanent = false)
    {
        var danceCenter = spawnPosition.ToVector3();

        var dancing = new HashSet<ulong>();
        var activeDancers = new Dictionary<ulong, Player>();
        var startedAt = Environment.TickCount64;
        var playback = new Dictionary<ulong, BoomboxDancePlayback>();
        var transformed = new HashSet<ulong>();
        var transformReapplyAt = new Dictionary<ulong, long>();
        var sharedPlayback = BoomboxDancePlayback.CreateSynchronized(definition);
        var zoneDances = DanceStates.GetValue(zone, _ => new ZoneDances());
        lock (zoneDances)
            zoneDances.Sources[boomboxNpc.Guid] = new(danceCenter, definition, permanent ? long.MaxValue : startedAt + definition.DurationMs);
        var independentNextAt = new Dictionary<ulong, long>();
        var wasPlaying = false;
        var expired = false;

        boomboxNpc.UpdateEveryTickAction = () => { lock (zoneDances) UpdateDanceTick(); };
        boomboxNpc.UpdateEverySecondAction = () => { lock (zoneDances) UpdateParticipants(); };

        void UpdateDanceTick()
        {
            if (expired)
                return;
            var now = Environment.TickCount64;
            foreach (var participant in zone.Players)
                zoneDances.SelectOwner(participant, now);
            UpdateParticipants();
            if (!permanent && now - startedAt >= definition.DurationMs)
                return;
            if (sharedPlayback is not null)
            {
                var targets = zone.Players.Where(p => dancing.Contains(p.Guid)
                    && p.BoomboxDanceOwner == boomboxNpc.Guid
                    && zoneDances.Selection.Owns(p.Guid, boomboxNpc.Guid)
                    && definition.DanceDurationsMs.ContainsKey(p.TemporaryAppearance != 0 ? p.TemporaryAppearance : p.Model)).ToList();
                if (targets.Count > 0 && !wasPlaying)
                    sharedPlayback.Resume();
                wasPlaying = targets.Count > 0;
                var animation = targets.Count > 0 ? sharedPlayback.Advance(now) : 0;
                if (animation != 0)
                {
                    foreach (var player in targets)
                    {
                        player.BoomboxDanceAnimation = animation;
                        zoneDances.Selection.Started(player.Guid, boomboxNpc.Guid, sharedPlayback.NextClipAt);
                    }
                    SyncDance(targets, animation);
                }
                return;
            }
            foreach (var player in zone.Players.Where(p => dancing.Contains(p.Guid)
                         && p.BoomboxDanceOwner == boomboxNpc.Guid
                         && zoneDances.Selection.Owns(p.Guid, boomboxNpc.Guid)))
            {
                if (definition.IndependentDanceDurationsMs.TryGetValue(player.Model, out var choices))
                {
                    if (now < independentNextAt.GetValueOrDefault(player.Guid))
                        continue;
                    var options = choices.Where(pair => pair.Key != player.BoomboxDanceAnimation).ToArray();
                    if (options.Length == 0)
                        options = choices.ToArray();
                    var selected = options[Random.Shared.Next(options.Length)];
                    var endsAt = now + selected.Value - Math.Clamp(definition.DanceBlendMs, 0, selected.Value - 1);
                    independentNextAt[player.Guid] = endsAt;
                    player.BoomboxDanceAnimation = selected.Key;
                    zoneDances.Selection.Started(player.Guid, boomboxNpc.Guid, endsAt);
                    SyncDance([player], selected.Key);
                    continue;
                }
                if (!playback.TryGetValue(player.Guid, out var clock))
                    continue;
                var animation = clock.Advance(now);
                if (animation != 0)
                {
                    player.BoomboxDanceAnimation = animation;
                    zoneDances.Selection.Started(player.Guid, boomboxNpc.Guid, clock.NextClipAt);
                    SyncDance([player], animation);
                }
            }
        }

        void UpdateParticipants()
        {
            if (expired)
                return;
            if (!permanent && Environment.TickCount64 - startedAt >= definition.DurationMs)
            {
                expired = true;
                zoneDances.Sources.Remove(boomboxNpc.Guid);
                foreach (var player in activeDancers.Values)
                    StopDancing(player, boomboxNpc.Guid, transformed.Contains(player.Guid) ? transformModelId : 0);

                if (songTagId != 0)
                {
                    var playerUpdatePacketRemoveEffectTagCompositeEffect = new PlayerUpdatePacketRemoveEffectTagCompositeEffect
                    {
                        Guid = boomboxNpc.Guid,
                        TagId = songTagId,
                    };

                    foreach (var player in zone.Players)
                        player.SendTunneled(playerUpdatePacketRemoveEffectTagCompositeEffect);
                }

                boomboxNpc.UpdateEveryTickAction = null;
                boomboxNpc.UpdateEverySecondAction = null;
                EntityHelper.RemovePlayerGracefully(boomboxNpc, compositeEffectId: definition.SpawnEffectId, duration: BoomboxRemoveDurationMs);
                return;
            }

            var players = zone.Players.ToList();
            var inRange = players.Where(p =>
                Vector3.Distance(p.Position.ToVector3(), danceCenter) <= definition.Range)
                .ToList();
            var inRangeGuids = inRange.Select(p => p.Guid).ToHashSet();

            foreach (var player in activeDancers.Values.Where(p => !inRangeGuids.Contains(p.Guid)))
            {
                StopDancing(player, boomboxNpc.Guid, transformed.Contains(player.Guid) ? transformModelId : 0);
                playback.Remove(player.Guid);
                independentNextAt.Remove(player.Guid);
                transformed.Remove(player.Guid);
                transformReapplyAt.Remove(player.Guid);
            }

            var now = Environment.TickCount64;
            var eligible = inRange.Where(p => (p.Mount is null || definition.ItemId == RealmsRollItemId)
                && zoneDances.SelectOwner(p, now) == boomboxNpc.Guid);
            var newcomers = eligible.Where(p => p.BoomboxDanceOwner != boomboxNpc.Guid).ToList();
            dancing = inRange.Where(p => p.Mount is null).Select(p => p.Guid).ToHashSet();
            activeDancers = inRange.ToDictionary(p => p.Guid);

            foreach (var player in newcomers)
            {
                if (player.BoomboxDanceOwner != 0 && definition.Priority > player.BoomboxDancePriority)
                    StopDancing(player, player.BoomboxDanceOwner, player.BoomboxDanceTransform);

                if (player.BoomboxDanceOwner != boomboxNpc.Guid && player.BoomboxDanceTransform != 0 && player.TemporaryAppearance == player.BoomboxDanceTransform)
                    player.RemoveTemporaryAppearance();
                if (player.BoomboxDanceOwner != boomboxNpc.Guid)
                    player.BoomboxDanceTransform = 0;
                player.BoomboxDanceOwner = boomboxNpc.Guid;
                player.BoomboxDancePriority = definition.Priority;
                player.BoomboxDanceAnimation = 0;
                player.BoomboxDanceIsStanding = !definition.SynchronizedDances && definition.StandingDanceAnimationId != 0
                    && definition.IndependentDanceDurationsMs.Count == 0;
                independentNextAt.Remove(player.Guid);
                if (transformModelId != 0 && player.TemporaryAppearance == 0 && definition.TransformReapplyDelayMs == 0)
                {
                    player.ApplyTemporaryAppearance(transformModelId,
                        (int)Math.Max(1, definition.DurationMs - (Environment.TickCount64 - startedAt)),
                        buffNameId: transformBuffNameId);
                    player.BoomboxDanceTransform = transformModelId;
                    transformed.Add(player.Guid);
                    transformReapplyAt.Remove(player.Guid);
                }

                if (!player.BoomboxDanceIsStanding)
                    player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
                    {
                        Guid = player.Guid,
                        AnimationId = IdleAnimationId,
                        Flags = 1
                    }, true);

                if (player.BoomboxDanceTransform != 0 && player.TemporaryAppearance == player.BoomboxDanceTransform)
                    StartRealmsRollDance(player);

                if (sharedPlayback is not null)
                    continue;

                if (definition.IndependentDanceDurationsMs.Count > 0)
                    continue;
                if (definition.StandingDanceAnimationId != 0)
                {
                    player.BoomboxDanceAnimation = definition.StandingDanceAnimationId;
                    player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
                    {
                        Guid = player.Guid,
                        AnimationId = definition.StandingDanceAnimationId,
                        Flags = 1
                    }, true);
                }
                else if (player.TemporaryAppearance == 0
                         && definition.DanceDurationsMs.TryGetValue(player.Model, out var durations))
                {
                    playback[player.Guid] = new BoomboxDancePlayback(definition.DanceSequence, durations, definition.DanceBlendMs);
                    var animation = playback[player.Guid].Advance(Environment.TickCount64);
                    player.BoomboxDanceAnimation = animation;
                    if (animation != 0)
                        SyncDance([player], animation);
                }
            }

            foreach (var player in inRange.Where(p => p.BoomboxDanceOwner == boomboxNpc.Guid))
            {
                if (player.Mount is not null)
                {
                    PauseDance(player);
                    continue;
                }

                if (player.BoomboxDanceAnimation != 0)
                    continue;

                if (player.BoomboxDanceTransform != 0 && player.TemporaryAppearance == player.BoomboxDanceTransform)
                    StartRealmsRollDance(player);
                else if (definition.StandingDanceAnimationId != 0 && !definition.SynchronizedDances
                         && definition.IndependentDanceDurationsMs.Count == 0)
                {
                    player.BoomboxDanceAnimation = definition.StandingDanceAnimationId;
                    player.BoomboxDanceIsStanding = true;
                    player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
                    {
                        Guid = player.Guid,
                        AnimationId = definition.StandingDanceAnimationId,
                        Flags = 1
                    }, true);
                }
            }
            if (transformModelId != 0 && definition.TransformReapplyDelayMs > 0)
            {
                foreach (var player in inRange.Where(p => p.BoomboxDanceOwner == boomboxNpc.Guid))
                {
                    if (definition.ItemId == RealmsRollItemId && player.BoomboxDanceIsStanding
                        && player.TemporaryAppearance != player.BoomboxDanceTransform)
                    {
                        player.BoomboxDanceIsStanding = false;
                        player.BoomboxDanceAnimation = 0;
                        player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
                        {
                            Guid = player.Guid,
                            AnimationId = IdleAnimationId,
                            Flags = 1
                        }, true);
                    }
                    if (player.TemporaryAppearance != 0)
                    {
                        transformReapplyAt.Remove(player.Guid);
                        continue;
                    }
                    var transformNow = Environment.TickCount64;
                    if (!transformReapplyAt.TryGetValue(player.Guid, out var reapplyAt))
                    {
                        transformReapplyAt[player.Guid] = transformNow + definition.TransformReapplyDelayMs;
                        continue;
                    }
                    if (transformNow < reapplyAt)
                        continue;
                    player.ApplyTemporaryAppearance(transformModelId,
                        (int)Math.Max(1, definition.DurationMs - (transformNow - startedAt)), buffNameId: transformBuffNameId);
                    player.BoomboxDanceTransform = transformModelId;
                    transformed.Add(player.Guid);
                    transformReapplyAt.Remove(player.Guid);
                    StartRealmsRollDance(player);
                }
            }

            if (songTagId != 0 && newcomers.Count > 0)
            {
                var playerUpdatePacketAddEffectTagCompositeEffect = new PlayerUpdatePacketAddEffectTagCompositeEffect
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

                    player.SendTunneled(playerUpdatePacketAddEffectTagCompositeEffect);
                }
            }

        }

        void StartRealmsRollDance(Player player)
        {
            if (definition.ItemId != RealmsRollItemId || definition.DanceSequence.Length == 0 || player.Mount is not null)
                return;

            player.BoomboxDanceAnimation = definition.DanceSequence[0];
            player.BoomboxDanceIsStanding = true;
            player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
            {
                Guid = player.Guid,
                AnimationId = player.BoomboxDanceAnimation,
                Flags = 1
            }, true);
        }
    }

    public static void PauseDance(Player player)
    {
        if (player.BoomboxDanceAnimation == 0)
            return;

        player.BoomboxDanceAnimation = 0;
        player.BoomboxDanceIsStanding = false;
        player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
        {
            Guid = player.Guid,
            AnimationId = IdleAnimationId,
            Flags = 1
        }, true);
        player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
        {
            Guid = player.Guid,
            AnimationId = IdleAnimationId,
            Unknown = 1
        }, true);
    }
    private static void SyncDance(List<Player> targets, int animationId)
    {
        if (targets.Count == 0)
            return;

        var playerUpdatePacketSetSynchronizedAnimations = new PlayerUpdatePacketSetSynchronizedAnimations();

        foreach (var player in targets)
            playerUpdatePacketSetSynchronizedAnimations.Animations.Add(new PlayerUpdatePacketSetSynchronizedAnimations.Animation { Guid = player.Guid, AnimationId = animationId });

        var recipients = new HashSet<Player>(targets);

        foreach (var player in targets)
            foreach (var (_, visiblePlayer) in player.VisiblePlayers)
                recipients.Add(visiblePlayer);

        var data = Player.SerializeTunneled(playerUpdatePacketSetSynchronizedAnimations);

        foreach (var recipient in recipients)
            recipient.SendSerialized(data);
    }

    private static void StopDancing(Player player, ulong owner, int transformModelId)
    {
        if (player.BoomboxDanceOwner != owner)
            return;
        player.BoomboxDanceOwner = 0;
        player.BoomboxDancePriority = 0;
        player.BoomboxDanceAnimation = 0;
        player.BoomboxDanceIsStanding = false;
        player.BoomboxDanceTransform = 0;
        if (transformModelId != 0 && player.TemporaryAppearance == transformModelId)
            player.RemoveTemporaryAppearance();

        player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation
        {
            Guid = player.Guid,
            AnimationId = IdleAnimationId,
            Flags = 1
        }, true);
    }

}
