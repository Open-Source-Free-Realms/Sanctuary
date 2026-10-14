using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

public class ProjectileHelper
{
    private readonly List<Projectile> _projectiles = [];

    public void AddProjectile(Player player, ulong guid, int animationId, int recoveryAnimationId, int recoveryDelayMs, int timeoutMs, int travelTimeMs)
    {
        player.SendTunneledToVisibleDelayed(new PlayerUpdatePacketSetAnimation
        {
            Guid = guid,
            AnimationId = animationId,
            Unknown = 1
        }, travelTimeMs, true);
        if (recoveryAnimationId != 0)
            player.SendTunneledToVisibleDelayed(new PlayerUpdatePacketSetAnimation
            {
                Guid = guid,
                AnimationId = recoveryAnimationId,
                Unknown = 1
            }, travelTimeMs + recoveryDelayMs, true);

        var now = DateTimeOffset.UtcNow;
        _projectiles.RemoveAll(x => x.ExpiresAt <= now);
        _projectiles.Add(new Projectile
        {
            Guid = guid,
            AnimationId = animationId,
            ExpiresAt = now.AddMilliseconds(timeoutMs)
        });
    }

    public bool HandleHit(Player player, AbilityPacketDetonateProjectile abilityPacketDetonateProjectile)
    {
        var now = DateTimeOffset.UtcNow;
        _projectiles.RemoveAll(x => x.ExpiresAt <= now);
        var projectile = _projectiles.FirstOrDefault(x => x.Guid == abilityPacketDetonateProjectile.Guid && x.AnimationId == abilityPacketDetonateProjectile.CompositeEffectId);
        if (projectile is null)
            return false;

        _projectiles.Remove(projectile);
        IEntity? target = player.VisiblePlayers.TryGetValue(abilityPacketDetonateProjectile.Guid, out var otherPlayer) ? otherPlayer :
            player.VisibleNpcs.TryGetValue(abilityPacketDetonateProjectile.Guid, out var npc) ? npc : null;
        return target is not null && ReferenceEquals(target.Zone, player.Zone);
    }

    private class Projectile
    {
        public ulong Guid { get; init; }
        public int AnimationId { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }
}
