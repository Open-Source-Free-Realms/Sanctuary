using System;
using System.Numerics;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.Entities;

/// <summary>
/// A decorative fish that slowly wanders within a radius of a home point on a pond/stream surface,
/// so the water is populated with visibly-swimming fish in the overworld (before/while fishing) — like
/// the fish you can see in the water in real Free Realms fishing holes.
///
/// Movement is server-driven: each tick the fish steps toward a random wander target and broadcasts its
/// new position to nearby players with <see cref="PlayerUpdatePacketUpdatePosition"/> (opcode 125) — the
/// same packet the server uses to relay player movement, keyed here to the fish's own NPC guid.
/// </summary>
public sealed class AmbientFishNpc : Npc
{
    private const float TickSeconds = 0.1f; // zone tick is 10 Hz (see BaseZone.FrameRate)

    private readonly Vector3 _home;
    private readonly float _radius;
    private readonly float _speed; // units per second
    private readonly Random _random;

    private Vector3 _target;
    private int _idleTicksLeft;

    public AmbientFishNpc(IZone zone, Vector3 home, float radius, float speed, int seed) : base(zone)
    {
        _home = home;
        _radius = radius;
        _speed = speed;
        _random = new Random(seed);
        _target = PickTarget();
    }

    /// <summary>A random point within the wander radius, on the water plane (varies X/Z, keeps the home Y).</summary>
    public Vector3 PickTarget()
    {
        var angle = _random.NextDouble() * Math.PI * 2.0;
        var dist = _radius * (float)Math.Sqrt(_random.NextDouble()); // sqrt = uniform over the disc
        return new Vector3(
            _home.X + (float)Math.Cos(angle) * dist,
            _home.Y,
            _home.Z + (float)Math.Sin(angle) * dist);
    }

    public override void UpdateEveryTick()
    {
        if (!Visible)
            return;

        // Pause briefly on arrival so the wandering doesn't look robotic.
        if (_idleTicksLeft > 0)
        {
            _idleTicksLeft--;
            return;
        }

        var pos = new Vector3(Position.X, Position.Y, Position.Z);
        var toTarget = _target - pos;
        var distance = toTarget.Length();
        var step = _speed * TickSeconds;

        var rotation = Rotation; // Npc.Rotation is set only via UpdatePosition, so carry it locally

        if (distance <= step || distance <= 0.0001f)
        {
            pos = _target;
            _target = PickTarget();
            _idleTicksLeft = _random.Next(5, 20); // ~0.5-2.0s settle before moving again
        }
        else
        {
            var dir = toTarget / distance;
            pos += dir * step;

            // Face the swim direction (yaw from the X/Z heading).
            var yaw = (float)Math.Atan2(dir.X, dir.Z);
            rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
        }

        var newPosition = new Vector4(pos.X, pos.Y, pos.Z, 1f);
        UpdatePosition(newPosition, rotation); // keeps our zone tile / visibility current as we roam

        var packet = new PlayerUpdatePacketUpdatePosition
        {
            Guid = Guid,
            Position = newPosition,
            Rotation = rotation,
            State = 1 // moving (players send a non-zero state while walking; 0 reads as idle/teleport)
        };

        // Broadcast to every player in the zone, not just our VisiblePlayers set: a fish learns about a
        // player only if that player was flagged Visible during the tile scan, which isn't guaranteed —
        // so relying on VisiblePlayers can silently drop the movement updates (fish render but never move).
        foreach (var player in Zone.Players)
            player.SendTunneled(packet);
    }
}
