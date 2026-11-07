using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Routines;
using Sanctuary.Packet;

namespace Sanctuary.Game.Quests;

public static class QuestScene
{
    private const int IdleAnimationId = 1;
    private const int IdleCharacterState = 1;
    private const byte SetBaseAnimation = 1;
    private const int MaxTalkMs = 7500;
    private const double MillisecondsPerSecond = 1000d;

    public static void Play(Player player, QuestSceneDefinition scene, string name)
    {
        var speakers = new Dictionary<int, ulong>();
        var actorGuids = new List<ulong>();

        foreach (var actor in scene.Actors)
        {
            var template = player.Zone.Npcs.FirstOrDefault(npc => npc.DefinitionId == actor.NpcId);
            if (template is null)
                continue;

            var guid = player.Zone.ReserveNpcGuid();

            var add = template.GetAddNpcPacket();
            add.Guid = guid;
            add.Position = new Vector4(actor.X, actor.Y, actor.Z, 1f);
            add.Rotation = QuaternionExtensions.FromHeading(actor.Heading);
            add.IsInteractable = false;

            player.SendTunneled(add);
            player.SendTunneled(new PlayerUpdatePacketUpdateCharacterState { Guid = guid, State = IdleCharacterState });

            speakers[actor.NpcId] = guid;
            actorGuids.Add(guid);
        }

        var talkMs = Math.Min(scene.LineDelayMs, MaxTalkMs);
        var steps = new List<Routine>();
        var lastAt = 0;

        for (var i = 0; i < scene.Lines.Count; i++)
        {
            var line = scene.Lines[i];

            if (!speakers.TryGetValue(line.NpcId, out var speakerGuid))
            {
                speakerGuid = NearestNpcGuid(player, line.NpcId);
                if (speakerGuid == 0)
                    continue;

                speakers[line.NpcId] = speakerGuid;
            }

            var at = line.DelayMs ?? scene.StartDelayMs + i * scene.LineDelayMs;
            var animationId = line.AnimationId ?? QuestDialogue.TalkAnimationId;

            lastAt = Math.Max(lastAt, at);

            steps.Add(At(at, () =>
            {
                if (animationId != 0)
                    SetAnimation(player, speakerGuid, animationId);

                player.SendTunneled(new ChatPacketFromStringId
                {
                    SpeakerGuid = speakerGuid,
                    StringId = line.TextId,
                    TargetGuid = player.Guid
                });
            }));

            if (animationId != 0)
                steps.Add(At(at + talkMs, () => SetAnimation(player, speakerGuid, IdleAnimationId)));
        }

        if (actorGuids.Count > 0)
            steps.Add(At(lastAt + scene.LineDelayMs, () => RemoveActors(player, actorGuids)));

        if (steps.Count == 0)
            return;

        player.Routines.SetRoutine(name, new SceneRoutine(new ParallelRoutine(steps), () => RemoveActors(player, actorGuids)));
    }

    private static DelayedRoutine At(int ms, Action action)
        => new(new InstantRoutine(action), ms / MillisecondsPerSecond);

    private static void SetAnimation(Player player, ulong guid, int animationId)
    {
        player.SendTunneled(new PlayerUpdatePacketSetAnimation
        {
            Guid = guid,
            AnimationId = animationId,
            Flags = SetBaseAnimation
        });
    }

    private static void RemoveActors(Player player, List<ulong> actorGuids)
    {
        foreach (var guid in actorGuids)
            player.SendTunneled(EntityHelper.GetRemovePlayerGracefullyPacket(guid, animate: true));

        actorGuids.Clear();
    }

    private static ulong NearestNpcGuid(Player player, int npcId)
    {
        Npc? nearest = null;
        var best = float.MaxValue;

        foreach (var npc in player.Zone.Npcs)
        {
            if (npc.DefinitionId != npcId)
                continue;

            var dx = npc.Position.X - player.Position.X;
            var dz = npc.Position.Z - player.Position.Z;
            var distance = dx * dx + dz * dz;

            if (distance < best)
            {
                best = distance;
                nearest = npc;
            }
        }

        return nearest?.Guid ?? 0;
    }

    private sealed class SceneRoutine : Routine
    {
        private readonly Routine _inner;
        private readonly Action _onInterrupted;

        public SceneRoutine(Routine inner, Action onInterrupted)
        {
            _inner = inner;
            _onInterrupted = onInterrupted;
        }

        protected override void OnStart() => _inner.Start();

        protected override bool OnStep(TimeSpan delta) => _inner.Step(delta);

        protected override void OnEnd() { }

        protected override void OnInterrupted()
        {
            _inner.Interrupt();
            _onInterrupted();
        }
    }
}
