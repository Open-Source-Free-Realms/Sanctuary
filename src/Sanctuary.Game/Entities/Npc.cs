using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Sanctuary.Game.Zones;
using Sanctuary.Game.Interactions;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Entities;

public class Npc : IEntity
{
    public ulong Guid { get; init; }

    public Vector4 Position { get; private set; }
    public Quaternion Rotation { get; private set; }

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

    

    // --- Command-spawn metadata (used for interaction permissions)
    public bool IsCommandSpawned { get; set; }
    public ulong SpawnedByGuid { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
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

    public bool HasHealthBar { get; set; }
    public int MaxHitpoints { get; set; }
    public int CurrentHitpoints { get; set; }

    public int MaxHealth
    {
        get => MaxHitpoints;
        set => MaxHitpoints = value;
    }

    public int Health
    {
        get => CurrentHitpoints;
        set => CurrentHitpoints = value;
    }

    public bool ShowHealthBar
    {
        get => HasHealthBar;
        set => HasHealthBar = value;
    }

    public bool IsHostile => Disposition == 0;
    public bool IsDamageable => MaxHitpoints > 0;
    public bool IsAlive => !IsDamageable || CurrentHitpoints > 0;

    public bool ApplyDamage(int amount)
    {
        if (!IsAlive)
            return false;

        CurrentHitpoints = Math.Max(0, CurrentHitpoints - amount);

        return CurrentHitpoints == 0;
    }

    // public NotificationInfo? Notification { get; set; }

    public List<CharacterAttachmentData> Attachments { get; set; } = [];

    public bool Static { get; set; }

    public Npc(IZone zone)
    {
        Zone = zone;
    }

    #region Events

    public void OnInteract(Player player)
{
    if (!IsInteractable)
        return;

    var listPacket = new CommandPacketInteractionList();
    listPacket.List.Guid = Guid;

    if (EnterHeweyEscapeInteraction.IsHeweyEscapeMarker(this))
    {
        var encounterDetails = new EncounterDetailsResponsePacket
        {
            EncounterType = 7,
            EncounterId = 20103,
            NameId = NameId != 0 ? NameId : 102010,
            DescriptionId = 0,
            Difficulty = 1,
            DetailImageId = 34069,
            ThumbnailImageId = 34070,
            RewardPreviewBundleId = 0
        };

        player.SendTunneled(new EncounterRunningListResponsePacket
        {
            Encounters = { encounterDetails }
        });
        player.SendTunneled(encounterDetails);
        player.SendTunneled(new ActivityPacketListOfActivities
        {
            Activities =
            {
                new()
                {
                    Id = 20103,
                    DisplayNameId = NameId != 0 ? NameId : 102010,
                    DisplayDescriptionId = SubTextNameId,
                    NameId = NameId != 0 ? NameId : 102010,
                    DescriptionId = SubTextNameId,
                    Category = 1,
                    ImageSetId = ImageSetId != 0 ? ImageSetId : 4199,
                    ActivityPositionId = 20103,
                    CanPlayerJoin = 1,
                    Difficulty = 1,
                    AppSystemId = 1,
                    DetailImageFilename = "heweys_escape_detail.dds",
                    ThumbnailImageFilename = "heweys_escape_thumb.dds",
                    MinigameDetail = 1
                }
            }
        });
        player.SendTunneled(new ExecuteScriptPacket
        {
            Script = "MinigameDetail.Show"
        });
        player.SendTunneled(new ExecuteScriptPacket
        {
            Script = "MinigameDetail.Populate",
            Params = { 20103 }
        });
        return;
    }

    // Keep Inspect (shows menu + inspect option)
    listPacket.List.Interactions.Add(InspectInteraction.Data);

    // Add Remove button ONLY for the spawner on command-spawned NPCs
            if (player.IsAdmin)
            listPacket.List.Interactions.Add(RemoveNpcInteraction.Data);

        player.SendTunneled(listPacket);
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
    }

    public virtual void UpdateEverySecond()
    {
    }

    public void UpdatePosition(Vector4 position, Quaternion rotation)
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

            Speed = default,

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
            Unknown41 = HasHealthBar,
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
    // Force-remove this NPC from every player currently in the zone.
    foreach (var player in Zone.Players)
    {
        player.OnRemoveVisibleNpcs([this]);
    }

    // Clean up NPC-to-player visibility tracking too.
    foreach (var visiblePlayer in VisiblePlayers.Values)
    {
        OnRemoveVisiblePlayers([visiblePlayer]);
    }

    ZoneTile.Entities.Remove(Guid, out _);
    Zone.TryRemoveNpc(Guid);
}
}

