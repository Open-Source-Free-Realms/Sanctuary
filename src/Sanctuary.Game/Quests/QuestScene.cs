using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;

namespace Sanctuary.Game.Quests;

public static class QuestScene
{
    private const int IdleAnimationId = 1;
    private const int IdleCharacterState = 1;
    private const byte SetBaseAnimation = 1;
    private const int MaxTalkMs = 7500;

    public static void Play(Player player, QuestSceneDefinition scene)
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

            var at = scene.StartDelayMs + i * scene.LineDelayMs;

            player.SendTunneledDelayed(new PlayerUpdatePacketSetAnimation
            {
                Guid = speakerGuid,
                AnimationId = QuestDialogue.TalkAnimationId,
                Flags = SetBaseAnimation
            }, at);

            player.SendTunneledDelayed(new ChatPacketFromStringId
            {
                SpeakerGuid = speakerGuid,
                StringId = line.TextId,
                TargetGuid = player.Guid
            }, at);

            player.SendTunneledDelayed(new PlayerUpdatePacketSetAnimation
            {
                Guid = speakerGuid,
                AnimationId = IdleAnimationId,
                Flags = SetBaseAnimation
            }, at + talkMs);
        }

        var endAt = scene.StartDelayMs + scene.Lines.Count * scene.LineDelayMs;

        foreach (var guid in actorGuids)
            player.SendTunneledDelayed(EntityHelper.GetRemovePlayerGracefullyPacket(guid, animate: true), endAt);
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
}
