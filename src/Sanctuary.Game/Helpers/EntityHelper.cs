using System.Collections.Generic;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

public static class EntityHelper
{
    private const float QueueAnimationUnknown3 = 1f;
    private const byte SetBaseAnimationFlags = 3;
    private const int PlayOnce = 0;
    private const byte PlayNowFlags = 2;
    private const int DefaultRemoveDurationMs = 1000;

    public static void RemovePlayerGracefully(IEntity entity, bool animate = false, int delay = 0,
        int effectDelay = 0, int compositeEffectId = 0, int duration = DefaultRemoveDurationMs, bool sendToSelf = false,
        IEnumerable<Player>? recipients = null)
    {
        var visiblePlayers = new HashSet<Player>(recipients ?? entity.VisiblePlayers.Values);

        if (sendToSelf && entity is Player player)
            visiblePlayers.Add(player);

        var playerUpdatePacketRemovePlayerGracefully = GetRemovePlayerGracefullyPacket(entity.Guid, animate, delay, effectDelay,
            compositeEffectId, duration);

        var data = Player.SerializeTunneled(playerUpdatePacketRemovePlayerGracefully);

        foreach (var visiblePlayer in visiblePlayers)
        {
            visiblePlayer.SendSerialized(data);

            if (entity is Npc)
                visiblePlayer.VisibleNpcs.TryRemove(entity.Guid, out _);
            else if (entity is Player)
                visiblePlayer.VisiblePlayers.TryRemove(entity.Guid, out _);
        }

        entity.VisiblePlayers.Clear();
        entity.Dispose();
    }

    public static PlayerUpdatePacketRemovePlayerGracefully GetRemovePlayerGracefullyPacket(ulong guid, bool animate = false, int delay = 0,
        int effectDelay = 0, int compositeEffectId = 0, int duration = DefaultRemoveDurationMs) => new()
    {
        Guid = guid,
        Animate = animate,
        Delay = delay,
        EffectDelay = effectDelay,
        CompositeEffectId = compositeEffectId,
        Duration = duration
    };

    public static void QueueAnimation(IEntity entity, int animationGroupId, int loopCount, bool unknown6, bool unknown7, bool unknown8)
    {
        var playerUpdatePacketQueueAnimation = new PlayerUpdatePacketQueueAnimation
        {
            Guid = entity.Guid,
            AnimationGroupId = animationGroupId,
            LoopCount = loopCount,
            Unknown3 = QueueAnimationUnknown3,
            Unknown6 = unknown6,
            Unknown7 = unknown7,
            Unknown8 = unknown8
        };
        var data = Player.SerializeTunneled(playerUpdatePacketQueueAnimation);

        foreach (var (_, visiblePlayer) in entity.VisiblePlayers)
            visiblePlayer.SendSerialized(data);
    }

    public static void SetBaseAnimation(Player owner, IEntity entity, int animationId)
    {
        var playerUpdatePacketSetAnimation = new PlayerUpdatePacketSetAnimation
        {
            Guid = entity.Guid,
            AnimationId = animationId,
            Flags = SetBaseAnimationFlags
        };

        owner.SendTunneledToVisible(playerUpdatePacketSetAnimation, true);
    }

    public static void SetAnimation(Player owner, IEntity entity, int animationId)
    {
        var playerUpdatePacketSetAnimation = new PlayerUpdatePacketSetAnimation
        {
            Guid = entity.Guid,
            AnimationId = animationId,
            Unknown = PlayOnce,
            Flags = PlayNowFlags
        };

        owner.SendTunneledToVisible(playerUpdatePacketSetAnimation, true);
    }

    public static void SetSpeed(Player owner, Npc npc, float speed)
    {
        if (npc.Speed == speed)
            return;

        npc.Speed = speed;

        var playerUpdatePacketExpectedSpeed = new PlayerUpdatePacketExpectedSpeed
        {
            Guid = npc.Guid,
            ExpectedSpeed = speed
        };

        var data = Player.SerializeTunneled(playerUpdatePacketExpectedSpeed);

        foreach (var (_, visiblePlayer) in npc.VisiblePlayers)
            visiblePlayer.SendSerialized(data);

        if (!npc.VisiblePlayers.ContainsKey(owner.Guid))
            owner.SendSerialized(data);
    }

    public static void SendThoughtBubble(Player owner, IEntity entity, int iconId, int seconds)
    {
        owner.SendTunneledToVisible(new PlayerUpdatePacketThoughtBubble
        {
            Guid = entity.Guid,
            IconId = iconId,
            DurationSeconds = seconds
        }, true);
    }

    public static void ClearThoughtBubble(Player owner, IEntity entity)
    {
        owner.SendTunneledToVisible(new PlayerUpdatePacketThoughtBubble
        {
            Guid = entity.Guid,
            Clear = true
        }, true);
    }

    public static void SetWalkAnimation(Player owner, IEntity entity, int animationGroupId)
    {
        owner.SendTunneledToVisible(new PlayerUpdatePacketUpdateWalkAnim
        {
            Guid = entity.Guid,
            Animation = animationGroupId
        }, true);
    }
}
