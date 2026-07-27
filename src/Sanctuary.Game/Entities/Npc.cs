using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System;

using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Game.Pathfinding;

namespace Sanctuary.Game.Entities;


public class Npc : IEntity
{
    public ulong Guid { get; init; }

    public Vector4 Position { get; private set; }
    public Quaternion Rotation { get; private set; }

    public Vector4 SpawnPosition { get; set; }
    public Quaternion SpawnRotation { get; set; }

    public bool Visible { get; set; }

    public IZone Zone { get; set; }
    public ZoneTile ZoneTile { get; protected set; } = ZoneTile.Empty;
    public ConcurrentDictionary<ulong, Npc> VisibleNpcs { get; } = [];
    public ConcurrentDictionary<ulong, Player> VisiblePlayers { get; } = [];

    public int NameId { get; set; }
    public string? Name { get; set; }
    public int SubTextNameId { get; set; }
    public bool HideNamePlate { get; set; }
    public int NameplateImageId { get; set; }
    public float VerticalOffset { get; set; }

    public int ModelId { get; set; }
    public int TerrainObjectId { get; set; }

    public string? TextureAlias { get; set; }
    public string? TintAlias { get; set; }
    public int TintId { get; set; }

    public float Scale { get; set; }

    /// <summary>
    /// 0 - Hostile
    /// 1 - Neutral
    /// 2 - Ally
    /// </summary>
    public int Disposition { get; set; } = 1;

    public int Animation { get; set; } = 1;

    public int CompositeEffectId { get; set; }

    public int InteractRange { get; set; } = 100;
    public bool IsInteractable { get; set; } = true;

    public int MovementType { get; set; }

    public int AreaDefinitionId { get; set; }

    public int ImageSetId { get; set; }

    public byte CursorId { get; set; }

    public NotificationInfo? Notification { get; set; }

    public List<CharacterAttachmentData> Attachments { get; set; } = [];

    public bool Static { get; set; }

    // TODO: Is this safe, scalable and okay for many, many NPCs? I (Alko) should think
    // more about this, but leaving it for now.
    public Queue<Vector3> Waypoints { get; set; } = new();

    private const float TickDeltaSeconds = 0.1f;

    // TODO: maybe keeping this constant is a bad idea... 
    // Could be different for all NPCs, fix this before the PR goes in.
    public const float NodeTolerance = 0.5f;
    public const float Speed = 6.25f;

    public MovementGoal? Goal { get; set; }
    private List<MapNode> _lastComputedPath = new();

    public Npc(IZone zone)
    {
        Zone = zone;
    }

    #region Events

    public void OnInteract(Player player)
    {
    }

    public virtual void OnAddVisibleNpcs(params IEnumerable<Npc> npcs)
    {
        foreach (var npc in npcs)
            VisibleNpcs.TryAdd(npc.Guid, npc);
    }

    public virtual void OnAddVisiblePlayers(params IEnumerable<Player> players)
    {
        foreach (var player in players)
            VisiblePlayers.TryAdd(player.Guid, player);
    }

    public virtual void OnRemoveVisibleNpcs(params IEnumerable<Npc> npcs)
    {
        foreach (var npc in npcs)
            VisibleNpcs.TryRemove(npc.Guid, out _);
    }

    public virtual void OnRemoveVisiblePlayers(params IEnumerable<Player> players)
    {
        foreach (var player in players)
            VisiblePlayers.TryRemove(player.Guid, out _);
    }

    #endregion

    #region Update

    public virtual void UpdateEveryTick()
    {
        // TODO: Perhaps all of this logic should live elsewhere and just get called here.
        // Feels like this is just bloating a function that is fairly importable.

        if (this.Waypoints.Count == 0)
        {
            return;
        }

        // NOTE: I (Alko) am not super duper familiar with how game engine physics affects NPC
        // movements. If path following is complete garbage for some reason (I doubt it will be)
        // come back and here and break out some basic robotics algorithms.
        var targetPosition = this.Waypoints.Peek();
        var currentPosition = new Vector3(this.Position.X, this.Position.Y, this.Position.Z);
        var toTarget = targetPosition - currentPosition;


        var distance = toTarget.Length();
        var direction = Vector3.Normalize(toTarget);

        if (distance <= NodeTolerance)
        {
            Waypoints.Dequeue();
            if (Waypoints.Count == 0 && Goal?.ClearOnArrival == true)
                Goal = null;
            return;
        }

        var step = direction * Speed * TickDeltaSeconds;
        if (step.Length() > distance)
        {
            step = toTarget;
        }

        var newPosition = currentPosition + step;
        var newOrientation = Quaternion.CreateFromYawPitchRoll(MathF.Atan2(direction.X, direction.Z), 0f, 0f);

        UpdatePosition(new Vector4(newPosition, 1f), newOrientation);
        BroadcastPosition();
    }

    public virtual void UpdateEverySecond()
    {
        // TODO: How well does this scale? Do we eventually want concurrency/parallelization (didn't really
        // look and see if something like that is already handeled at the higher-level).
        RecomputePath();
    }

    public void UpdatePosition(Vector4 position, Quaternion rotation, bool updateZoneArea = true)
    {
        Position = position;
        Rotation = rotation;

        if (Visible)
        {
            UpdateZoneTile();
        }
    }

    public virtual void TeleportToZone(IZone zone, Vector4 position, Quaternion rotation)
    {
    }

    protected void UpdateZoneTile()
    {
        var newZoneTile = Zone.GetTileFromPosition(Position);

        if (newZoneTile == ZoneTile)
            return;

        Zone.UpdateEntityZoneTile(this, ZoneTile, newZoneTile);

        ZoneTile = newZoneTile;
    }

    #endregion

    public virtual PlayerUpdatePacketAddNpc GetAddNpcPacket()
    {
        var packet = new PlayerUpdatePacketAddNpc
        {
            Guid = Guid,

            NameId = NameId,

            ModelId = ModelId,

            Unknown = default,

            TextureAlias = TextureAlias,
            TintAlias = TintAlias,

            TintId = TintId,

            Scale = Scale,

            Position = Position,
            Rotation = Rotation,

            Attachments = Attachments,
            HasAttachments = Attachments.Count > 0,

            Disposition = Disposition,

            Animation = Animation,

            Unknown16 = default,
            VerticalOffset = VerticalOffset,

            CompositeEffectId = CompositeEffectId,

            WieldType = default,

            Name = Name,

            HideNamePlate = HideNamePlate,

            Unknown22 = default,
            Unknown23 = default,
            Unknown24 = default,

            TerrainObjectId = TerrainObjectId,

            Speed = Speed,

            Unknown28 = default,

            InteractRange = InteractRange,

            WalkAnimId = default, // Walk GroupAnimId
            RunAnimId = default, // Sprint GroupAnimId
            StandAnimId = default, // Idle GroupAnimId

            Unknown33 = default,
            Unknown34 = default,

            SubTextNameId = SubTextNameId,

            Unknown36 = default, // AnimationEvent
            TemporaryAppearance = default,

            // playerUpdatePacketAddNpc.EffectTags = TODO

            Unknown38 = default,
            Unknown39 = default,
            Unknown40 = default,
            Unknown41 = default,
            Unknown42 = default,

            HasTilt = default,

            // playerUpdatePacketAddNpc.Customization = TODO

            Tilt = default,

            NameColor = default,

            AreaDefinitionId = AreaDefinitionId,

            ImageSetId = ImageSetId,

            IsInteractable = IsInteractable,

            RiderGuid = default,

            MovementType = MovementType,

            Unknown51 = default,

            Unknown52 = default,

            Unknown53 = default,

            Unknown54 = default,

            Unknown55 = default,

            Unknown56 = default,
            Unknown57 = default,
            Unknown58 = default,

            // playerUpdatePacketAddNpc.Head = TODO
            // playerUpdatePacketAddNpc.Hair = TODO
            // playerUpdatePacketAddNpc.ModelCustomization = TODO

            ReplaceTerrainObject = default,

            Unknown63 = default,
            Unknown64 = 3050,

            FlyByEffectId = default,

            ActiveProfile = default,

            Unknown67 = default,
            Unknown68 = default,

            NameScale = default,

            NameplateImageId = NameplateImageId
        };

        return packet;
    }

    #region Equatable

    public bool Equals(IEntity? other)
    {
        return Guid == other?.Guid;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is Npc other)
            return Equals(other);

        return false;
    }

    public override int GetHashCode()
    {
        return Guid.GetHashCode();
    }

    public static bool operator ==(Npc left, Npc right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Npc left, Npc right)
    {
        return !(left == right);
    }

    #endregion

    public virtual void Dispose()
    {
        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.OnRemoveVisibleNpcs([this]);

        RemoveFromZone();
    }

    protected void DisposeGracefully(bool animate, int delay, int effectDelay, int compositeEffectId, int duration)
    {
        foreach (var visiblePlayer in VisiblePlayers)
        {
            visiblePlayer.Value.OnRemoveVisibleNpcGracefully(
                this, animate, delay, effectDelay, compositeEffectId, duration);
        }

        RemoveFromZone();
    }

    private void RemoveFromZone()
    {
        ZoneTile.Entities.Remove(Guid, out _);

        Zone.TryRemoveNpc(Guid);
    }

    public void MoveTo(Vector3 goalPosition)
    {
        Goal = new MovementGoal.FixedPosition(goalPosition);
        RecomputePath();
    }

    public void Chase(Player player)
    {
        // TODO: not stable
        throw new NotSupportedException("NPC chasing is not yet ready.");
    }

    private void RecomputePath()
    {


        if (Goal is null || Zone.Pathfinder is null)
            return;

        var currentPosition = Waypoints.Count > 0
            ? Waypoints.Peek()
            : new Vector3(Position.X, Position.Y, Position.Z);

        var goalPosition = Goal.GetPosition();
        var path = Zone.Pathfinder.FindPath(currentPosition, goalPosition);

        if (IsSimilarPath(_lastComputedPath, path))
            return;

        _lastComputedPath = path;

        Waypoints = new Queue<Vector3>();

        // NOTE: This may not be stable in practice. We might want to detour to the nearest
        // node in some cases, but until we get navmesh graphs, I think this will be fine.
        if (path.Count <= 1)
        {
            Waypoints.Enqueue(goalPosition);
            return;
        }

        foreach (var node in path)
            Waypoints.Enqueue(node.Position);

        Waypoints.Enqueue(goalPosition);
    }

    private void BroadcastPosition()
    {
        var packet = new PlayerUpdatePacketUpdatePosition
        {
            Guid = this.Guid,
            Position = this.Position,
            Rotation = this.Rotation,
            State = 1,
            Unknown = 0
        };

        foreach (var visiblePlayer in VisiblePlayers)
        {
            visiblePlayer.Value.SendTunneled(packet);
        }
    }

    private static bool IsSimilarPath(List<MapNode> oldPath, List<MapNode> newPath)
    {
        if (oldPath.Count == 0 || newPath.Count == 0)
            return false;

        var overlap = Math.Min(oldPath.Count, newPath.Count);

        for (var i = 1; i <= overlap; i++)
        {
            if (oldPath[^i].Id != newPath[^i].Id)
                return false;
        }

        return true;
    }
}
