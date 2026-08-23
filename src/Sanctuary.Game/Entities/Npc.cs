using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Actions;
using Sanctuary.Core.Collections;
using Sanctuary.Game.Actions;
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

    public IZone Zone { get; set; }
    public ZoneTile ZoneTile { get; protected set; } = ZoneTile.Empty;
    public ConcurrentDictionary<ulong, Npc> VisibleNpcs { get; } = [];
    public ConcurrentDictionary<ulong, Player> VisiblePlayers { get; } = [];
    public ILogger Logger => Zone.Logger;
    public ScriptRuntime ScriptRuntime => Zone.ScriptRuntime;

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

    public int MovementType => 2;

    public int AreaDefinitionId { get; set; }

    public int ImageSetId { get; set; }

    public byte CursorId { get; set; }

    public NotificationInfo? Notification { get; set; }

    public List<CharacterAttachmentData> Attachments { get; set; } = [];


    private ConcurrentSet<string> _scripts { get; } = [];

    public float WaypointTolerance { get; set; } = 0f;
    public float Speed { get; set; } = 6.25f;

    private readonly ActionManager _actions = new();

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

    public void UpdateEveryTick()
    {
        if (!_scripts.IsEmpty)
            GetOrCreateScriptContext().FireEvent("tick");

        _actions.Tick();
    }

    public void UpdateEverySecond()
    {
        if (!_scripts.IsEmpty)
            GetOrCreateScriptContext().FireEvent("second");
    }

    public void UpdatePosition(Vector4 position, Quaternion rotation, bool updateZoneArea = true)
    {
        Position = position;
        Rotation = rotation;

        if (Visible)
        {
            UpdateZoneTile();
        }

        var packet = new PlayerUpdatePacketUpdatePosition
        {
            Guid = Guid,
            Position = position,
            Rotation = rotation,
            State = 1,
            Unknown = 0
        };

        foreach (var visiblePlayer in VisiblePlayers)
        {
            visiblePlayer.Value.SendTunneled(packet);
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
        var packet = new PacketChat
        {
            Channel = ChatChannel.WorldSay,
            FromGuid = Guid,
            FromName = new NameData { FirstName = Name ?? string.Empty },
            Message = message
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(packet);
    }

    public void SayLocalized(int stringId)
    {
        var packet = new ChatPacketFromStringId
        {
            SpeakerGuid = Guid,
            StringId = stringId
        };

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendTunneled(packet);
    }

    public IAction MoveTo(float x, float y, float z, bool direct)
    {
        return MoveTo(new Vector3(x, y, z), direct);
    }

    public void SetAction(string slot, IAction action) => _actions.SetAction(slot, action);

    #endregion

    public virtual void Dispose()
    {
        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.OnRemoveVisibleNpcs([this]);

        RemoveFromZone();

        Zone.ScriptManager.DeleteContext(this);
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

    public IAction MoveTo(Vector3 goalPosition, bool direct = false)
    {
        return new MoveToAction(this, goalPosition, direct);
    }
}
