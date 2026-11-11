using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Routines;
using Sanctuary.Packet;

namespace Sanctuary.Game.Quests;

public static class QuestScene
{
    private const int IdleCharacterState = 1;
    private const double MillisecondsPerSecond = 1000d;

    public static void Play(Player player, QuestSceneDefinition scene, string name, ILogger logger)
    {
        var speakers = new Dictionary<int, ulong>();
        var actorGuids = new List<ulong>();

        foreach (var actor in scene.Actors)
        {
            var template = player.Zone.Npcs.FirstOrDefault(npc => npc.DefinitionId == actor.NpcId);
            if (template is null)
            {
                logger.LogWarning("Scene {name} has no NPC {npcId} in the zone to copy, so that actor is skipped.", name, actor.NpcId);
                continue;
            }

            var guid = player.Zone.ReserveNpcGuid();

            var add = template.GetAddNpcPacket();
            add.Guid = guid;
            add.Position = actor.Position;
            add.Rotation = QuaternionExtensions.FromHeading(actor.Heading);
            add.IsInteractable = false;

            player.SendTunneled(add);
            player.SendTunneled(new PlayerUpdatePacketUpdateCharacterState { Guid = guid, State = IdleCharacterState });

            speakers[actor.NpcId] = guid;
            actorGuids.Add(guid);
        }

        var cues = new List<(int AtMs, Action Play)>();

        for (var i = 0; i < scene.Lines.Count; i++)
        {
            var line = scene.Lines[i];

            if (!speakers.TryGetValue(line.NpcId, out var speakerGuid))
            {
                speakerGuid = EntityHelper.NearestNpcGuid(player, line.NpcId);
                if (speakerGuid == 0)
                {
                    logger.LogWarning("Scene {name} has no NPC {npcId} in the zone to speak line {textId}, so that line is skipped.", name, line.NpcId, line.TextId);
                    continue;
                }

                speakers[line.NpcId] = speakerGuid;
            }

            var animationId = line.AnimationId ?? QuestDialogue.TalkAnimationId;

            cues.Add((line.DelayMs ?? scene.StartDelayMs + i * scene.LineDelayMs, () =>
            {
                if (animationId != 0)
                    EntityHelper.SendAnimation(player, speakerGuid, animationId);

                player.SendTunneled(new ChatPacketFromStringId
                {
                    SpeakerGuid = speakerGuid,
                    StringId = line.TextId,
                    TargetGuid = player.Guid
                });
            }));
        }

        if (cues.Count == 0 && actorGuids.Count == 0)
            return;

        var steps = new List<Routine>();
        var elapsedMs = 0;

        foreach (var cue in cues.OrderBy(cue => cue.AtMs))
        {
            if (cue.AtMs > elapsedMs)
            {
                steps.Add(new WaitRoutine((cue.AtMs - elapsedMs) / MillisecondsPerSecond));
                elapsedMs = cue.AtMs;
            }

            steps.Add(new InstantRoutine(cue.Play));
        }

        if (actorGuids.Count > 0)
            steps.Add(new WaitRoutine(scene.LineDelayMs / MillisecondsPerSecond));

        var sequence = new SequentialRoutine(steps);

        // TODO: DelegateRoutine needs an onStep override that takes a Func<TimeSpan, bool> so a routine's Step can be passed through without the zone tick delta.
        // TODO: DelegateRoutine should run onStart right before the first onStep call.
        player.Routines.SetRoutine(name,
            onStep: () => sequence.Step(player.Zone.TickDelta),
            onStart: sequence.Start,
            onEnd: () => RemoveActors(player, actorGuids));
    }

    private static void RemoveActors(Player player, List<ulong> actorGuids)
    {
        foreach (var guid in actorGuids)
            player.SendTunneled(EntityHelper.GetRemovePlayerGracefullyPacket(guid, animate: true));

        actorGuids.Clear();
    }
}
