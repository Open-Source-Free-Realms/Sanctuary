using System;
using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Packet;

namespace Sanctuary.Game.Entities;

public sealed class NpcAmbientController
{
    private readonly Npc _npc;
    private const byte IdleAnimationFlags = 3;

    private long _nextAnimationAt;
    private long _nextIdleAnimationAt;
    private int _idleAnimationIndex;
    private long _nextLookAtAt;

    public NpcAmbientController(Npc npc, NpcAmbientDefinition definition)
    {
        _npc = npc;
        Definition = definition;
        _nextAnimationAt = Environment.TickCount64 + GetIntervalMs();
    }

    public NpcAmbientDefinition Definition { get; }

    public NpcConversationGroup? ConversationGroup { get; set; }

    public Vector3 Position => _npc.Position.ToVector3();

    public void Update()
    {
        UpdateLookAt();
        UpdateIdleAnimation();

        if (ConversationGroup is not null)
        {
            ConversationGroup.Update(this);
            return;
        }

        if (_npc.IsMoving || Environment.TickCount64 < _nextAnimationAt)
            return;

        _nextAnimationAt = Environment.TickCount64 + GetIntervalMs();

        if (Definition.AnimationIds.Count > 0)
            PlayAnimation(Definition.AnimationIds[Random.Shared.Next(Definition.AnimationIds.Count)]);
    }

    public void PlayAnimation(int animationId, byte flags = 0)
    {
        if (_npc.VisiblePlayers.IsEmpty)
            return;

        var playerUpdatePacketSetAnimation = new PlayerUpdatePacketSetAnimation
        {
            Guid = _npc.Guid,
            AnimationId = animationId,
            Flags = flags
        };

        var data = Player.SerializeTunneled(playerUpdatePacketSetAnimation);

        foreach (var visiblePlayer in _npc.VisiblePlayers)
            visiblePlayer.Value.SendSerialized(data);
    }

    private void UpdateIdleAnimation()
    {
        if (Definition.IdleAnimationIds.Count == 0 || _npc.IsMoving || Environment.TickCount64 < _nextIdleAnimationAt)
            return;

        _nextIdleAnimationAt = Environment.TickCount64 + Definition.IdleIntervalMs;

        if (_npc.VisiblePlayers.IsEmpty)
            return;

        var animationId = Definition.IdleAnimationIds[_idleAnimationIndex];
        _idleAnimationIndex = (_idleAnimationIndex + 1) % Definition.IdleAnimationIds.Count;

        var playerUpdatePacketUpdateIdleAnim = new PlayerUpdatePacketUpdateIdleAnim
        {
            Guid = _npc.Guid,
            AnimationId = animationId
        };

        var data = Player.SerializeTunneled(playerUpdatePacketUpdateIdleAnim);

        foreach (var visiblePlayer in _npc.VisiblePlayers)
            visiblePlayer.Value.SendSerialized(data);

        PlayAnimation(animationId, IdleAnimationFlags);
    }

    private void UpdateLookAt()
    {
        if (Definition.LookAtRadius <= 0 || Environment.TickCount64 < _nextLookAtAt)
            return;

        _nextLookAtAt = Environment.TickCount64 + Definition.LookAtIntervalMs;

        var targetGuid = 0UL;
        var nearbyCount = 0;

        foreach (var visiblePlayer in _npc.VisiblePlayers)
        {
            if (visiblePlayer.Value.Position.IsInRange(_npc.Position, Definition.LookAtRadius) && Random.Shared.Next(++nearbyCount) == 0)
                targetGuid = visiblePlayer.Key;
        }

        if (nearbyCount == 0)
            return;

        var playerUpdatePacketSetLookAt = new PlayerUpdatePacketSetLookAt
        {
            Guid = _npc.Guid,
            TargetGuid = targetGuid
        };

        var data = Player.SerializeTunneled(playerUpdatePacketSetLookAt);

        foreach (var visiblePlayer in _npc.VisiblePlayers)
            visiblePlayer.Value.SendSerialized(data);
    }

    private int GetIntervalMs() =>
        Random.Shared.Next(Definition.MinIntervalMs, Math.Max(Definition.MinIntervalMs, Definition.MaxIntervalMs) + 1);
}

public sealed class NpcConversationGroup
{
    private readonly List<NpcAmbientController> _members;
    private int _turn;
    private long _nextTurnAt;

    public NpcConversationGroup(List<NpcAmbientController> members)
    {
        _members = members;
        _nextTurnAt = Environment.TickCount64 + GetGapMs(_members[0].Definition);
    }

    public void Update(NpcAmbientController member)
    {
        if (!ReferenceEquals(member, _members[0]) || Environment.TickCount64 < _nextTurnAt)
            return;

        var speaker = _members[_turn];
        _turn = (_turn + 1) % _members.Count;

        var conversationAnimationIds = speaker.Definition.ConversationAnimationIds;
        if (conversationAnimationIds.Count > 0)
            speaker.PlayAnimation(conversationAnimationIds[Random.Shared.Next(conversationAnimationIds.Count)]);

        _nextTurnAt = Environment.TickCount64 + GetGapMs(speaker.Definition);
    }

    private static int GetGapMs(NpcAmbientDefinition definition) =>
        Random.Shared.Next(definition.ConversationMinGapMs, Math.Max(definition.ConversationMinGapMs, definition.ConversationMaxGapMs) + 1);
}
