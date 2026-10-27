using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Conversations;
using Sanctuary.Game.Interactions;
using Sanctuary.Core.Extensions;
using Sanctuary.Game.Pathfinding;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;
using Sanctuary.Scripting;

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
    public ScriptRuntime ScriptRuntime => Zone.ScriptRuntime;

    public int DefinitionId { get; set; }

    public int NameId { get; set; }
    public string? Name { get; set; }
    public int SubTextNameId { get; set; }
    public bool HideNamePlate { get; set; }
    public int NameplateImageId { get; set; }
    public float VerticalOffset { get; set; }

    public int ModelId { get; set; }
    public int TerrainObjectId { get; set; }
    public bool ReplaceTerrainObject { get; set; }

    /// <summary>
    /// Furniture in a house, which is edited with the house's own tools.
    /// </summary>
    public bool IsHouseFixture { get; set; }

    /// <summary>
    /// A prop placed into a world zone by a GM (a world fixture).
    /// </summary>
    public bool IsWorldProp { get; set; }

    /// <summary>
    /// GM-authored script (see INpcContentManager).
    /// </summary>
    public ScriptSandbox? GmScript { get; set; }

    /// <summary>
    /// GM-authored conversation (see INpcContentManager).
    /// </summary>
    public Conversation? Conversation { get; set; }

    /// <summary>
    /// Players currently inside the script's onProximity range. Lock it to use it.
    /// </summary>
    public HashSet<ulong> NearbyPlayers { get; } = [];

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

    /// <summary>
    /// Clicking does something for a regular player.
    /// </summary>
    public virtual bool HasInteraction => Interactions.Count > 0 || InteractAction is not null || OpensAtlas;

    public bool HasNameplate => !HideNamePlate && (!string.IsNullOrEmpty(Name) || NameId != 0);

    /// <summary>
    /// Whether the client shows its glow and "Press X or click" prompt: only for NPCs that do something
    /// when clicked and are either named or a deliberate clickable object with their own cursor (e.g. a house exit).
    /// </summary>
    public bool ShowsInteractPrompt => IsInteractable && HasInteraction && (HasNameplate || CursorId != 0);

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

    public float WaypointTolerance { get; set; } = 0f;
    public float Speed { get; set; } = 6.25f;

    public int PositionUpdateIntervalMs { get; set; }

    public NpcMovementController? MovementController { get; set; }

    public NpcAmbientController? AmbientController { get; set; }

    public bool IsMoving => _pathMoving || !_path.IsEmpty;

    private const int HostileDisposition = 0;
    private const byte MovingPositionState = 0;
    private const byte StoppedPositionState = 1;
    private const float GroundStepHeight = 3f;
    private const float GroundMaxDrop = 20f;

    private readonly PathState _path = new();
    private bool _pathMoving;
    private long _nextPositionSendAt;

    private PathBuilder? _pathBuilder;

    public Npc(IZone zone)
    {
        Zone = zone;
    }

    #region Events

    public void OnInteract(Player player)
    {
        if (player.IsInGodMode && RefereeNpcInteractions.CanEdit(this))
        {
            RefereeEditorHelper.Select(player, this);
            player.SendTunneled(InteractionMenuHelper.GetInteractionListPacket(Guid, Name, RefereeNpcInteractions.GetMenu(this)));
            return;
        }

        if (InteractionMenuHelper.TryOpenMenu(player, this, Zone.GetInteractionOptions(player, this)))
            return;

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
        UpdateEveryTickAction?.Invoke();
        if (!_scripts.IsEmpty)
            GetOrCreateScriptContext().FireEvent("tick");

        MovementController?.Update();
        AmbientController?.Update();

        var currentPosition = Position.ToVector3();
        var result = PathFollower.Advance(_path, currentPosition, Speed, WaypointTolerance, Zone.TickDeltaSeconds);

        if (result.Moved)
        {
            _pathMoving = true;
            var rotation = result.NewRotation!.Value;
            var newPosition = Zone.GetGroundPosition(result.NewPosition, GroundStepHeight, GroundMaxDrop);

            if (PositionUpdateIntervalMs == 0 || Environment.TickCount64 >= _nextPositionSendAt)
            {
                _nextPositionSendAt = Environment.TickCount64 + PositionUpdateIntervalMs;
                UpdatePosition(newPosition.ToVector4(), rotation, MovingPositionState);
            }
            else
            {
                Position = newPosition.ToVector4();
                Rotation = rotation;

                if (Visible)
                    UpdateZoneTile();
            }
        }
        else if (_pathMoving && result.Arrived)
        {
            _pathMoving = false;
            UpdatePosition(Position, Rotation, StoppedPositionState);
        }
    }

    public void UpdateEverySecond()
    {
        UpdateEverySecondAction?.Invoke();

        UpdateBehavior();

        if (GmScript is not null || Conversation is not null)
            Zone.NpcContent.OnSecond(this);

        if (!_scripts.IsEmpty)
            GetOrCreateScriptContext().FireEvent("second");
    }

    protected virtual bool SendsPositionTo(Player player) => true;

    public const int FollowerPositionIntervalMs = 500;

    public int PositionSendIntervalMs { get; set; }
    private long _nextPositionSendAllowedAt;
    private byte _lastSentPositionState;

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

    public void UpdatePosition(Vector4 position, Quaternion rotation) =>
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

        if (PositionSendIntervalMs > 0)
        {
            var now = Environment.TickCount64;

            if (state == _lastSentPositionState && now < _nextPositionSendAllowedAt)
                return;

            _lastSentPositionState = state;
            _nextPositionSendAllowedAt = now + PositionSendIntervalMs;
        }

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

            IsInteractable = ShowsInteractPrompt,

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

            NotificationImageSetId = default,
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

    public void Emote(int animationId, bool loop) => PlayAnimation(animationId, loop);

    public void Face(IScriptablePlayer player) => FaceTowards(new Vector4(player.Position.x, player.Position.y, player.Position.z, 1f));

    public void StartConversation(IScriptablePlayer player, int? nodeId)
    {
        if (player is ScriptPlayer scriptPlayer)
            Zone.NpcContent.StartConversation(scriptPlayer.Player, this, nodeId);
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

    private const int ScriptedWalkUpdateIntervalMs = 1000;

    private const float ScriptedWalkWaypointTolerance = 0.25f;

    public void SetSpeed(float speed)
    {
        Speed = speed;

        if (PositionUpdateIntervalMs == 0)
            PositionUpdateIntervalMs = ScriptedWalkUpdateIntervalMs;

        if (WaypointTolerance == 0)
            WaypointTolerance = ScriptedWalkWaypointTolerance;
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

    #region Referee editing

    private const float FollowDistance = 3f;

    private NpcBehavior _behavior;
    private ulong _followGuid;
    private float _wanderRadius;
    private int _wanderCooldown;

    public NpcBehavior Behavior => _behavior;

    public float WanderRadius => _wanderRadius;

    /// <summary>
    /// Turns in place to face the position. Returns false if it is (almost) on top of us.
    /// </summary>
    public bool FaceTowards(Vector4 position)
    {
        var direction = new Vector3(position.X - Position.X, 0f, position.Z - Position.Z);

        if (direction.LengthSquared() < 0.0001f)
            return false;

        UpdatePosition(Position, Quaternion.CreateFromYawPitchRoll(MathF.Atan2(direction.X, direction.Z), 0f, 0f));
        return true;
    }

    public void SetBehavior(NpcBehavior behavior, ulong followGuid = 0, float wanderRadius = 10f)
    {
        _behavior = behavior;
        _followGuid = followGuid;
        _wanderRadius = wanderRadius;
        _wanderCooldown = 0;

        if (behavior == NpcBehavior.Stay)
            StopMoving();
    }

    private void UpdateBehavior()
    {
        switch (_behavior)
        {
            case NpcBehavior.Follow:
                if (!VisiblePlayers.TryGetValue(_followGuid, out var target))
                {
                    SetBehavior(NpcBehavior.Stay);
                    return;
                }

                if (Vector4.Distance(Position, target.Position) > FollowDistance)
                    MoveTo(new Vector3(target.Position.X, target.Position.Y, target.Position.Z));
                break;

            case NpcBehavior.Wander:
                if (IsMoving || --_wanderCooldown > 0)
                    return;

                var angle = Random.Shared.NextSingle() * MathF.Tau;
                var distance = Random.Shared.NextSingle() * _wanderRadius;
                var home = SpawnPosition == default ? Position : SpawnPosition;

                MoveTo(new Vector3(home.X + MathF.Cos(angle) * distance, home.Y, home.Z + MathF.Sin(angle) * distance));

                _wanderCooldown = Random.Shared.Next(3, 8);
                break;
        }
    }

    /// <summary>
    /// Plays an animation for everyone in view. A looping one also becomes the animation new viewers see.
    /// </summary>
    public void PlayAnimation(int animationId, bool loop)
    {
        if (loop)
            Animation = animationId;

        var packet = new PlayerUpdatePacketSetAnimation
        {
            Guid = Guid,
            AnimationId = animationId,
            Flags = (byte)(loop ? 1 : 0)
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(packet);
    }

    /// <summary>
    /// Re-sends this NPC to everyone in view, so changes that have no update packet of their own
    /// (name, scale, model, disposition, nameplate) show without a relog.
    /// </summary>
    public void RefreshForVisiblePlayers()
    {
        foreach (var visiblePlayer in VisiblePlayers)
        {
            visiblePlayer.Value.OnRemoveVisibleNpcs([this]);
            visiblePlayer.Value.OnAddVisibleNpcs([this]);
        }
    }

    #endregion

    public void MoveTo(Vector3 goalPosition, bool direct = false)
    {
        StartLeg();

        if (!direct)
        {
            var navMeshWaypoints = new Queue<Vector3>();

            if (Zone.TryFindRoute(Position.ToVector3(), goalPosition, navMeshWaypoints))
            {
                _path.Set(navMeshWaypoints);
                return;
            }
        }

        if (direct || Zone.Pathfinder is null)
        {
            _path.SetTarget(goalPosition);
            return;
        }

        _pathBuilder ??= new PathBuilder(Zone.Pathfinder);

        SetPath(goalPosition, _pathBuilder.TryRecompute(Position.ToVector3(), goalPosition));
    }

    public void MoveTo(Queue<Vector3> waypoints)
    {
        StartLeg();
        _path.Set(waypoints);
    }

    public void MoveTo(Vector3 goalPosition, List<MapNode> route)
    {
        if (Zone.Pathfinder is null)
        {
            _path.SetTarget(goalPosition);
            return;
        }

        _pathBuilder ??= new PathBuilder(Zone.Pathfinder);

        SetPath(goalPosition, _pathBuilder.TryRecompute(Position.ToVector3(), goalPosition, route));
    }

    public void StopMoving() => _path.Clear();

    private void StartLeg()
    {
        if (PositionUpdateIntervalMs == 0)
            return;

        _nextPositionSendAt = Environment.TickCount64;
        SendExpectedSpeed();
    }

    public void SetPositionY(float y) => Position = Position with { Y = y };

    private void SetPath(Vector3 goalPosition, Queue<Vector3>? newWaypoints)
    {
        if (newWaypoints is not null)
            _path.Set(newWaypoints);
        else
            _path.SetGoal(goalPosition);
    }
}
