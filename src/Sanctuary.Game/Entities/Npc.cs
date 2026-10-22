using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Core.Extensions;
using Sanctuary.Game.Pathing;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Routines;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;
using Sanctuary.Scripting;
using System.Security.Cryptography;

namespace Sanctuary.Game.Entities;


public class Npc : IScriptableNpc, IEntity
{
    public ulong Guid { get; init; }

    public Vector4 Position { get; private set; }
    public Quaternion Rotation { get; private set; }

    public Vector4 SpawnPosition { get; set; }
    public Quaternion SpawnRotation { get; set; }

    public bool Visible { get; set; }

    public bool Collidable { get; private set; } = true;

    public IZone Zone { get; set; }
    public ZoneTile ZoneTile { get; protected set; } = ZoneTile.Empty;
    public ConcurrentDictionary<ulong, Npc> VisibleNpcs { get; } = [];
    public ConcurrentDictionary<ulong, Player> VisiblePlayers { get; } = [];
    public ILogger Logger => Zone.Logger;

    public RoutineManager Routines { get; }
    public ScriptRuntime ScriptRuntime => Zone.ScriptRuntime;

    public int NameId { get; set; }
    public string? Name { get; set; }
    public int SubTextNameId { get; set; }
    public bool HideNamePlate { get; set; }
    public int NameplateImageId { get; set; }
    public float VerticalOffset { get; set; }

    public int ModelId { get; set; }
    public int TerrainObjectId { get; set; }
    public bool ReplaceTerrainObject { get; set; }

    public string? TextureAlias { get; set; }
    public string? TintAlias { get; set; }
    public int TintId { get; set; }

    public float Scale { get; set; } = 1f;

    public Vector4? Tilt { get; set; }

    /// <summary>
    /// 0 - Hostile
    /// 1 - Neutral
    /// 2 - Ally
    /// </summary>
    public int Disposition { get; set; } = 1;

    public Action<Player>? InteractAction { get; set; }
    public Action? UpdateEverySecondAction { get; set; }
    public Action? UpdateEveryTickAction { get; set; }

    public int Animation { get; set; } = 1;

    public int CompositeEffectId { get; set; }

    public int InteractRange { get; set; } = 100;
    public bool IsInteractable { get; set; } = true;
    public bool OpensAtlas { get; set; }
    public bool AutoSelectSingleInteraction { get; set; }
    public List<InteractionData> Interactions { get; } = [];

    private const int DefaultMovementType = 2;
    private const int PlacedObjectMovementType = 0;

    public int MovementType => Tilt.HasValue ? PlacedObjectMovementType : DefaultMovementType;

    public int AreaDefinitionId { get; set; }

    public int ImageSetId { get; set; }

    public byte CursorId { get; set; }
    public bool? HasCursor { get; set; }
    public bool RelevanceUnknown2 { get; set; }
    public InteractionList? InteractionList { get; set; }
    public bool InteractionUnknown { get; set; }
    public MerchantList? MerchantList { get; set; }
    public NpcAbilityDefinition? Ability { get; set; }

    public NotificationInfo? Notification { get; set; }
    public List<NotificationInfo> Notifications { get; set; } = [];

    public List<CharacterAttachmentData> Attachments { get; set; } = [];


    private ConcurrentSet<string> _scripts { get; } = [];

    public float Speed { get; set; } = 6.25f;

    public NpcMovementController? MovementController { get; set; }

    public NpcAmbientController? AmbientController { get; set; }

    private const int HostileDisposition = 0;

    // TODO: this can probably be an enum.
    private const byte MovingPositionState = 0;
    private const byte StoppedPositionState = 1;

    // TODO: Maybe this goes into 'json' config
    private const int MaxSearchIterations = 1024;

    // TODO: Rename and potentially make settable per NPC.
    private static readonly ChaseSettings TargetChaseSettings = new(
        0.5f, // Minimum distance to target (u).
        1.5f, // Maximum distance to target (u).
        MathF.PI / 2f, // Spread angle for catching (rad).
        0.2f, // Stopping tolerance (u).
        2f // Replanning frequency (Hz).
    );
    private const string MovementRoutineName = "Movement";
    private readonly Pathfinder? _pathfinder;
    private readonly PathFollower _pathFollower;

    // TODO: May be worth re-evaluating this check at a later date.
    public bool IsMoving => _pathFollower.HasPath;

    public Npc(IZone zone)
    {
        Zone = zone;

        Routines = new RoutineManager(Logger);

        // TODO: these need to be rebuilt/reset on zone change in the future.
        if (zone.NavMeshQuery is not null)
            _pathfinder = new Pathfinder(zone.NavMeshQuery, zone.NavMeshFilter, MaxSearchIterations);

        _pathFollower = new PathFollower(zone.NavMeshQuery);
    }

    #region Events

    public void OnInteract(Player player)
    {
        if (OpensAtlas)
        {
            var executeScriptPacket = OpenAtlasInteraction.GetAtlasPacket(this, player);
            if (executeScriptPacket is not null)
                player.SendTunneled(executeScriptPacket);
            return;
        }

        if (InteractionList is not null)
        {
            if (InteractionMenuHelper.CanInteract(this, player))
                player.SendTunneled(InteractionMenuHelper.GetInteractionListPacket(Guid, InteractionList, InteractionUnknown));
            return;
        }

        if (Interactions.Count > 0)
        {
            if (InteractionMenuHelper.CanInteract(this, player))
                player.SendTunneled(InteractionMenuHelper.GetInteractionListPacket(Guid, Name, Interactions, autoSelectSingle: AutoSelectSingleInteraction));
            return;
        }

        InteractAction?.Invoke(player);
    }

    public virtual bool IsVisibleTo(Player player) => true;

    public virtual void OnAddVisibleNpcs(params IEnumerable<Npc> npcs)
    {
        foreach (var npc in npcs)
            VisibleNpcs.TryAdd(npc.Guid, npc);
    }

    public virtual void OnAddVisiblePlayers(params IEnumerable<Player> players)
    {
        foreach (var player in players)
        {
            if (IsVisibleTo(player))
                VisiblePlayers.TryAdd(player.Guid, player);
        }
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

    public void UpdateEveryTick()
    {
        Routines.OnTick();

        UpdateEveryTickAction?.Invoke();
        if (!_scripts.IsEmpty)
            GetOrCreateScriptContext().FireEvent("tick");

        MovementController?.Update();
        AmbientController?.Update();
    }

    public void UpdateEverySecond()
    {
        Routines.OnSecond();

        UpdateEverySecondAction?.Invoke();

        if (!_scripts.IsEmpty)
            GetOrCreateScriptContext().FireEvent("second");
    }

    protected virtual bool SendsPositionTo(Player player) => true;

    public const float DefaultPositionUpdateFrequency = 4f;
    public const float FollowerPositionUpdateFrequency = 2f;

    public float PositionUpdateFrequency { get; set; } = DefaultPositionUpdateFrequency;
    private long _nextMovingPositionUpdateAt;
    private byte _lastSentPositionState = StoppedPositionState;

    private void SendExpectedSpeed()
    {
        if (VisiblePlayers.IsEmpty)
            return;

        var playerUpdatePacketExpectedSpeed = new PlayerUpdatePacketExpectedSpeed
        {
            Guid = Guid,
            ExpectedSpeed = Speed
        };

        var data = Player.SerializeTunneled(playerUpdatePacketExpectedSpeed);

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendSerialized(data);
    }

    public void UpdatePosition(Vector4 position, Quaternion rotation, bool updateZoneArea = true) =>
        UpdatePosition(position, rotation, StoppedPositionState);

    private void UpdatePosition(Vector4 position, Quaternion rotation, byte state)
    {

        Position = position;
        Rotation = rotation;

        if (Visible)
        {
            UpdateZoneTile();
        }

        var playerUpdatePacketUpdatePosition = new PlayerUpdatePacketUpdatePosition
        {
            Guid = Guid,
            Position = position,
            Rotation = rotation,
            State = state,
            Unknown = 0
        };

        var now = Environment.TickCount64;

        if (state == MovingPositionState && _lastSentPositionState == MovingPositionState
            && PositionUpdateFrequency > 0f && now < _nextMovingPositionUpdateAt)
            return;

        _lastSentPositionState = state;

        if (state == MovingPositionState && PositionUpdateFrequency > 0f)
            _nextMovingPositionUpdateAt = now + (long)(1000f / PositionUpdateFrequency);

        if (VisiblePlayers.IsEmpty)
            return;

        var data = Player.SerializeTunneled(playerUpdatePacketUpdatePosition);

        foreach (var visiblePlayer in VisiblePlayers)
        {
            if (!SendsPositionTo(visiblePlayer.Value))
                continue;

            visiblePlayer.Value.SendSerialized(data);
        }
    }

    public virtual bool TeleportToZone(IZone zone, Vector4 position, Quaternion rotation)
    {
        return true;
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
        var playerUpdatePacketAddNpc = new PlayerUpdatePacketAddNpc
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

            HideHealthBar = Disposition != HostileDisposition,
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
            Unknown34 = Tilt.HasValue,

            SubTextNameId = SubTextNameId,

            Unknown36 = default, // AnimationEvent
            TemporaryAppearance = default,

            // playerUpdatePacketAddNpc.EffectTags = TODO

            Unknown38 = default,
            Unknown39 = default,
            Unknown40 = default,
            Unknown41 = default,
            DisableCollision = !Collidable,

            HasTilt = Tilt.HasValue,

            // playerUpdatePacketAddNpc.Customization = TODO

            Tilt = Tilt ?? default,

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

            ReplaceTerrainObject = ReplaceTerrainObject,

            Unknown63 = default,
            Unknown64 = 3050,

            FlyByEffectId = default,

            ActiveProfile = default,

            Unknown67 = default,
            Unknown68 = default,

            NameScale = default,

            NameplateImageId = NameplateImageId
        };

        return playerUpdatePacketAddNpc;
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

    #region IScriptable

    public ScriptContext GetOrCreateScriptContext()
    {
        if (Zone.ScriptManager.GetOrCreateContext(this, out var context))
        {
            // Fresh context. Load all attached scripts into it.
            foreach (var scriptName in _scripts)
                context.LoadScriptInBackground(Path.Combine("Npc", scriptName + ".lua"));
        }

        return context;
    }

    public bool TryAddScript(string scriptName)
    {
        var context = GetOrCreateScriptContext();

        if (!_scripts.TryAdd(scriptName))
            return false;

        var scriptPath = Path.Combine("Npc", scriptName + ".lua");

        context.LoadScriptInBackground(scriptPath);

        return true;
    }

    public bool TryRemoveScript(string scriptName)
    {
        if (!_scripts.TryRemove(scriptName))
            return false;

        var context = GetOrCreateScriptContext();

        var scriptPath = Path.Combine("Npc", scriptName + ".lua");

        return context.UnloadScript(scriptPath);
    }

    #endregion

    #region Scripting API

    // Explicit interface implementation needed here to avoid exposing all of IZone to the scripting layer.
    IScriptableZone IScriptableNpc.Zone => Zone;

    (float x, float y, float z) IScriptableNpc.Position => (Position.X, Position.Y, Position.Z);

    public void Say(string message)
    {
        var packetChat = new PacketChat
        {
            Channel = ChatChannel.WorldSay,
            FromGuid = Guid,
            FromName = new NameData { FirstName = Name ?? string.Empty },
            Message = message
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(packetChat);
    }

    public void SayLocalized(int stringId)
    {
        var chatPacketFromStringId = new ChatPacketFromStringId
        {
            SpeakerGuid = Guid,
            StringId = stringId
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(chatPacketFromStringId);
    }

    public void MoveTo(float x, float y, float z, bool direct)
    {
        MoveTo(new Vector3(x, y, z), direct);
    }

    public bool MoveTo(Vector3 goalPosition, bool direct = false)
    {
        NavPath path;

        if (direct || _pathfinder is null)
            path = new NavPath([goalPosition], true);
        else if (!_pathfinder.TryFindPath(Position.ToVector3(), goalPosition, out path))
            return false;

        Routines.SetRoutine(
            MovementRoutineName,
            new DelegateRoutine(
                onStart: () => _pathFollower.SetPath(path, 0f), // TODO: Magic number!
                onStep: () => StepPathFollower(),
                onEnd: () => _pathFollower.Stop()
            ),
            Cadence.Tick
        );

        return true;
    }

    public void StopMoving()
    {
        var wasMoving = IsMoving;

        Routines.Cancel(MovementRoutineName);

        if (wasMoving)
            UpdatePosition(Position, Rotation, StoppedPositionState);
    }

    public void Chase(IEntity target)
    {
        if (_pathfinder is null)
            return;


        var chaseOffset = TargetChaseSettings.GetOffset(Position.ToVector3(), target.Position.ToVector3());

        // TODO: Make a periodic routine that just accepts an arbitrary routine...
        Routines.SetRoutine(
            MovementRoutineName,
            new ParallelRoutine(
                [
                    // NOTE: We can use a parallel routine to give us low-frequency behavior here. It'll have to wait for the
                    // 'WaitRoutine' to finish. Combining this with a repeat routine (infinite until stopped), we get a rate-limited
                    // motion planner. 
                    // 
                    // NOTE: Hard planning frequencies/periods are not enforced and we expect that these will diverge by about a tick
                    // due to rounding.
                    new RepeatRoutine( // Low-frequency planning
                        new ParallelRoutine(
                            [
                                new DelegateRoutine(onStep: () =>
                                    {
                                        var position = Position.ToVector3();
                                        var targetPosition = target.Position.ToVector3();



                                        if (_pathfinder.TryFindPath(position, targetPosition + chaseOffset, out var path))
                                            _pathFollower.SetPath(path, TargetChaseSettings.StopTolerance);

                                        return true;
                                    }
                                ),
                                new WaitRoutine(1f / TargetChaseSettings.ReplanFrequency)
                            ]
                        )
                    ),
                    new DelegateRoutine( // High-frequency following
                        // NOTE: We force return false here to keep the NPC always chasing. 
                        onStep: () =>
                        {
                            StepPathFollower();
                            return false;


                        },
                        onEnd: () => _pathFollower.Stop()
                    ),
                ]
            ),
            Cadence.Tick
        );
    }




    private bool StepPathFollower()
    {
        if (!_pathFollower.HasPath)
            return true;

        var currentPosition = Position.ToVector3();
        var newPosition = _pathFollower.Advance(currentPosition, Speed * Zone.TickDeltaSeconds, out var arrived);


        // TODO: maybe put this in the path follower directly and create a Pose record for entitys to use...
        var rotation = (newPosition - currentPosition).LengthSquared() > 0f ? (newPosition - currentPosition).ToFlatRotation() : Rotation;

        UpdatePosition(newPosition.ToVector4(), rotation, arrived ? StoppedPositionState : MovingPositionState);

        return arrived;
    }

    private const byte SetBaseAnimationFlag = 1;

    public void SetBaseAnimation(int animationId)
    {
        Animation = animationId;

        var playerUpdatePacketSetAnimation = new PlayerUpdatePacketSetAnimation
        {
            Guid = Guid,
            AnimationId = animationId,
            Flags = SetBaseAnimationFlag
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(playerUpdatePacketSetAnimation);
    }

    public void PlayAnimation(int animationId)
    {
        var playerUpdatePacketSetAnimation = new PlayerUpdatePacketSetAnimation
        {
            Guid = Guid,
            AnimationId = animationId
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(playerUpdatePacketSetAnimation);
    }

    public IReadOnlyList<ulong> GetNearbyPlayerGuids(float radius)
    {
        var nearbyPlayerGuids = new List<ulong>();

        foreach (var visiblePlayer in VisiblePlayers)
        {
            if (visiblePlayer.Value.Position.IsInRange(Position, radius))
                nearbyPlayerGuids.Add(visiblePlayer.Key);
        }

        return nearbyPlayerGuids;
    }

    public IReadOnlyList<ulong> GetPlayerGuidsOnTop(float horizontalRadius, float minHeight, float maxHeight)
    {
        var playerGuidsOnTop = new List<ulong>();

        foreach (var visiblePlayer in VisiblePlayers)
        {
            var offset = visiblePlayer.Value.Position - Position;
            var horizontalDistance = MathF.Sqrt(offset.X * offset.X + offset.Z * offset.Z);

            if (horizontalDistance <= horizontalRadius && offset.Y >= minHeight && offset.Y <= maxHeight)
                playerGuidsOnTop.Add(visiblePlayer.Key);
        }

        return playerGuidsOnTop;
    }

    public void SetCollidable(bool collidable)
    {
        Collidable = collidable;

        var playerUpdatePacketSetCollidable = new PlayerUpdatePacketSetCollidable
        {
            Guid = Guid,
            Collidable = collidable
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(playerUpdatePacketSetCollidable);
    }

    public int AddEffectTag(int compositeEffectId)
    {
        var tagId = EffectTagIdGenerator.Next();

        var playerUpdatePacketAddEffectTagCompositeEffect = new PlayerUpdatePacketAddEffectTagCompositeEffect
        {
            Guid = Guid,
            TagId = tagId,
            CompositeEffectId = compositeEffectId,
            SourceGuid = Guid
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(playerUpdatePacketAddEffectTagCompositeEffect);

        return tagId;
    }

    public void RemoveEffectTag(int tagId)
    {
        var playerUpdatePacketRemoveEffectTagCompositeEffect = new PlayerUpdatePacketRemoveEffectTagCompositeEffect
        {
            Guid = Guid,
            TagId = tagId
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(playerUpdatePacketRemoveEffectTagCompositeEffect);
    }

    public void SetSpeed(float speed)
    {
        if (Speed == speed)
            return;

        Speed = speed;
        SendExpectedSpeed();
    }

    public void SetHeading(float heading) =>
        UpdatePosition(Position, QuaternionExtensions.FromHeading(heading), StoppedPositionState);

    public void PlayEffect(int compositeEffectId)
    {
        var playerUpdatePacketPlayCompositeEffect = new PlayerUpdatePacketPlayCompositeEffect
        {
            Guid = Guid,
            CompositeEffectId = compositeEffectId,
            Position = Position
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(playerUpdatePacketPlayCompositeEffect);
    }

    public void Remove(int compositeEffectId) => EntityHelper.RemovePlayerGracefully(this, compositeEffectId: compositeEffectId);

    #endregion

    public virtual void Dispose()
    {
        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.OnRemoveVisibleNpcs([this]);

        RemoveFromZone();

        Zone.ScriptManager.DeleteContext(this);
    }

    private void RemoveFromZone()
    {
        ZoneTile.Entities.Remove(Guid, out _);

        Zone.TryRemoveNpc(Guid);
    }

    public void SetPositionY(float y) => Position = Position with { Y = y };
}
