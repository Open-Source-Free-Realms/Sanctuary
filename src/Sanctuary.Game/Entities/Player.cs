using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Sanctuary.Core.Collections;
using Sanctuary.Core.Extensions;
using Sanctuary.Core.IO;
using Sanctuary.Core.Helpers;
using Sanctuary.Game;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Resources.Definitions.Combat;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.QuestGoals;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Game.Entities;

public sealed class Player : ClientPcData, IEntity
{
    private enum PacketSendType
    {
        All,
        AllAndSelf,
        SelfOnly
    }

    private const int WeaponSlot = 7;
    private const int FlairShardSlot = 13;
    private const int IdleCharacterState = 1;
    private const int ActionBarSlotUnknown5 = 1;
    private const int ActionBarSlotUnknown6 = 4;
    private const int ActionBarSlotUnknown7 = 15;
    private const float OnFootMovementSpeed = 8f;

    private readonly UdpConnection _connection;
    private readonly IResourceManager _resourceManager;
    private readonly IZoneManager _zoneManager;

    public bool Visible { get; set; }

    public IZone Zone { get; set; }
    public ZoneTile ZoneTile { get; private set; } = ZoneTile.Empty;
    public ConcurrentDictionary<ulong, Npc> VisibleNpcs { get; } = [];
    public ConcurrentDictionary<ulong, Player> VisiblePlayers { get; } = [];

    private uint? _portraitCrc;
    private uint? _pendingPortraitCrc;
    private DateTimeOffset _portraitUpdateExpiresAt;

    private int ZoneAreaId { get; set; }

    public int ChatBubbleForegroundColor { get; set; }
    public int ChatBubbleBackgroundColor { get; set; }
    public int ChatBubbleSize { get; set; }

    public bool IsAdmin { get; set; }
    public bool IsMod { get; set; }
    public ChatCommandRole ChatCommandRole => ChatHelper.GetRoleFromFlags(IsAdmin, IsMod);
    public DateTimeOffset? MutedUntil { get; set; }

    public ClientPcProfile ActiveProfile =>
        Profiles.FirstOrDefault(x => x.Id == ActiveProfileId) ?? Profiles.First();

    public Mount? Mount { get; set; }
    public PlayerPet PlayerPet { get; }
    public Npc? Pet => PlayerPet.Pet;
    public Dictionary<int, int> PetDefinitions { get; } = [];
    public Dictionary<int, PetTrait> PetTraits { get; } = [];
    public Dictionary<int, DateTimeOffset> PetTreasureFoundAt { get; } = [];

    public PetClientInfo? GetPetClientInfo(int petId) => Pets.FirstOrDefault(petClientInfo => petClientInfo.Id == petId);

    public PetClientInfo? GetActivePetClientInfo() => GetPetClientInfo(ActivePetId);

    public List<FriendData> Friends { get; set; } = [];
    public List<IgnoreData> Ignores { get; set; } = [];

    public ConcurrentSet<ulong> IncomingFriendRequests { get; } = [];
    public ConcurrentSet<ulong> IncomingGuildInvites { get; } = [];


    public ulong CharacterId { get; set; }

    public ulong LastInteractNpcGuid { get; set; }
    public DateTime LastInteractAt { get; set; }

    public DateTime LastQuestAcceptedAt { get; set; }

    public Dictionary<int, bool> Quests { get; } = new();

    public Dictionary<int, int> QuestGoalProgress { get; } = new();

    public Dictionary<int, int> QuestCollectProgress { get; } = new();

    public HashSet<int> TalkedQuestNpcs { get; } = new();

    public Queue<QuestDialogueLine> PendingDialogue { get; } = new();
    public ulong PendingDialogueNpcGuid { get; set; }

    public ulong TalkingNpcGuid { get; set; }
    public int TalkAnimationTicket { get; set; }

    public int ActiveQuestId { get; set; }

    public System.Action? PendingQuestEndAction { get; set; }

    public ConcurrentDictionary<ChatChannel, bool> ChatChannelStatus { get; set; } = [];

    public DateTimeOffset LoginStartedAt { get; set; }

    public long NextClientPathRequestAt { get; set; }
    public int SecondsSinceLastLogin { get; set; } = -1;
    public ConcurrentSet<int> CollectedCollectionEntryIds { get; } = [];

    public int StationCash { get; set; }
    public List<CoinStoreTransactionRecord> CoinStoreTransactions { get; set; } = [];

    public GuildData? GuildData { get; set; }

    public int TimezoneOffset { get; set; }

    public Dictionary<int, Dictionary<int, int>> ActionBarItemGuids { get; set; } = new();

    public int TemporaryAppearance { get; private set; }

    public int BoomboxDancePriority { get; set; }
    public ulong BoomboxDanceOwner { get; set; }
    public byte PositionState { get; set; }

    private const int CrowdedVisiblePlayerCount = 50;
    private const int CrowdedRelayIntervalMs = 500;

    public RelayedPosition? LatestPosition { get; private set; }
    private long _positionVersion;
    private readonly ConcurrentDictionary<ulong, (long Version, byte State, long NextDueAt)> _sentPositions = new();
    public int BoomboxDanceAnimation { get; set; }
    public bool BoomboxDanceIsStanding { get; set; }
    public int BoomboxDanceTransform { get; set; }

    public const float DefaultScale = 1f;

    public float Scale { get; private set; } = DefaultScale;

    public ulong LastSillyStringTarget { get; set; }

    public int ActiveFoodEffectId { get; set; }

    private readonly ConcurrentDictionary<int, (DateTimeOffset ExpiresAt, int TotalMs)> _itemCooldowns = new();

    public bool IsItemOnCooldown(int itemDefinitionId) =>
        _itemCooldowns.TryGetValue(itemDefinitionId, out var cooldown) && DateTimeOffset.UtcNow < cooldown.ExpiresAt;

    public void StartItemCooldown(int itemDefinitionId, int cooldownMs) =>
        _itemCooldowns[itemDefinitionId] = (DateTimeOffset.UtcNow.AddMilliseconds(cooldownMs), cooldownMs);

    public bool TryGetItemCooldown(int itemDefinitionId, out int totalMs, out int elapsedMs)
    {
        if (_itemCooldowns.TryGetValue(itemDefinitionId, out var cooldown) && DateTimeOffset.UtcNow < cooldown.ExpiresAt)
        {
            totalMs = cooldown.TotalMs;
            elapsedMs = totalMs - (int)(cooldown.ExpiresAt - DateTimeOffset.UtcNow).TotalMilliseconds;
            return true;
        }

        totalMs = 0;
        elapsedMs = 0;
        return false;
    }

    private readonly ConcurrentDictionary<int, int> _lastRandomTransformIndex = new();

    public int LastRandomTransformIndex(int itemDefinitionId) =>
        _lastRandomTransformIndex.TryGetValue(itemDefinitionId, out var index) ? index : -1;

    public void SetLastRandomTransformIndex(int itemDefinitionId, int index) =>
        _lastRandomTransformIndex[itemDefinitionId] = index;

    // For one-off delayed packets unrelated to tracked effects (e.g. a silly string reset
    // animation, a boombox poof) - see PlayerEffect/AddEffect for anything with its own state.
    // Min-heap ordered by send time.
    private readonly PriorityQueue<(ISerializablePacket Packet, PacketSendType SendType), DateTimeOffset> _delayedPackets = new();

    // One scheduled personal-UI packet per action bar slot (the cooldown re-enable) - keyed, not queued,
    // so a slot that gets emptied before its cooldown naturally expires (last item consumed) can cancel
    // its own pending re-enable instead of it firing later and silently un-deleting the slot.
    private readonly ConcurrentDictionary<(int, int), (DateTimeOffset SendAt, ISerializablePacket Packet)> _delayedSlotPackets = new();

    public void ScheduleSlotPacket(int actionBarId, int slotIndex, ISerializablePacket serializablePacket, int delayMs)
    {
        _delayedSlotPackets[(actionBarId, slotIndex)] = (DateTimeOffset.UtcNow.AddMilliseconds(delayMs), serializablePacket);
    }

    public void CancelScheduledSlotPacket(int actionBarId, int slotIndex) => _delayedSlotPackets.TryRemove((actionBarId, slotIndex), out _);

    public Vector4 StartingZonePosition { get; set; }
    public Quaternion StartingZoneRotation { get; set; }

    public ConcurrentSet<OneTimeNotification> SeenOneTimeNotifications { get; } = [];

    public bool ShopDataSent { get; set; }
    public ConcurrentSet<int> SentCoinStoreItemDefinitionIds { get; } = [];

    public Player(BaseZone zone, UdpConnection connection, IResourceManager resourceManager, IZoneManager zoneManager)
    {
        Zone = zone;

        _connection = connection;
        _resourceManager = resourceManager;
        _zoneManager = zoneManager;
        PlayerPet = new PlayerPet(this, resourceManager);
    }

    #region Connection

    public void Send(ISerializablePacket serializablePacket)
    {
        var data = serializablePacket.Serialize();

        _connection.Send(UdpChannel.Reliable1, data);
    }

    internal void SendSerialized(byte[] data)
    {
        _connection.Send(UdpChannel.Reliable1, data);
    }

    internal static byte[] SerializeTunneled(ISerializablePacket serializablePacket)
    {
        var packetTunneledClientPacket = new PacketTunneledClientPacket
        {
            Payload = serializablePacket.Serialize()
        };

        return packetTunneledClientPacket.Serialize();
    }

    public void SendToVisible(ISerializablePacket serializablePacket, bool sendToSelf = false)
    {
        if (VisiblePlayers.IsEmpty && !sendToSelf)
            return;

        var data = serializablePacket.Serialize();

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendSerialized(data);

        if (sendToSelf)
            SendSerialized(data);
    }

    public void SendTunneled(ISerializablePacket serializablePacket)
    {
        var packetTunneledClientPacket = new PacketTunneledClientPacket
        {
            Payload = serializablePacket.Serialize()
        };

        Send(packetTunneledClientPacket);
    }

    [Obsolete]
    public void SendTunneled(byte[] buffer)
    {
        var packetTunneledClientPacket = new PacketTunneledClientPacket
        {
            Payload = buffer
        };

        Send(packetTunneledClientPacket);
    }

    public void SendTunneledToVisible(ISerializablePacket serializablePacket, bool sendToSelf = false)
    {
        if (VisiblePlayers.IsEmpty && !sendToSelf)
            return;

        var data = SerializeTunneled(serializablePacket);

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.SendSerialized(data);

        if (sendToSelf)
            SendSerialized(data);
    }

    public void SendTunneledToVisibleDelayed(ISerializablePacket serializablePacket, int delayMs, bool sendToSelf = false)
    {
        lock (_delayedPackets)
        {
            var sendType = sendToSelf ? PacketSendType.AllAndSelf : PacketSendType.All;
            _delayedPackets.Enqueue((serializablePacket, sendType), DateTimeOffset.UtcNow.AddMilliseconds(delayMs));
        }
    }

    public void SendTunneledDelayed(ISerializablePacket packet, int delayMs)
    {
        lock (_delayedPackets)
        {
            _delayedPackets.Enqueue((packet, PacketSendType.SelfOnly), DateTimeOffset.UtcNow.AddMilliseconds(delayMs));
        }
    }

    public bool IsMuted()
    {
        DateTimeOffset currentTime = DateTimeOffset.UtcNow;
        DateTimeOffset? mutedUntil = MutedUntil;
        return mutedUntil.HasValue && mutedUntil > currentTime;
    }

    public void Disconnect()
    {
        _connection.Disconnect();
    }

    public void Dismount()
    {
        if (Mount is null)
            return;

        SendTunneledToVisible(new PacketDismountResponse
        {
            RiderGuid = Guid,
            CompositeEffectId = 46
        }, sendToSelf: true);

        UpdateCharacterStats(
            CharacterStats.GlideEnabled.Set(0),
            CharacterStats.JumpHeight.Set(0f));

        SetMountMovementSpeed(null);

        EntityHelper.RemovePlayerGracefully(Mount, recipients: VisiblePlayers.Values.Append(this));

        foreach (var mountInfo in Mounts)
        {
            if (mountInfo.Guid == Mount.Guid)
                mountInfo.Guid = 0;
        }

        Mount = null;
        if (BoomboxDanceTransform != 0 && TemporaryAppearance == BoomboxDanceTransform)
            SendTunneledToVisible(new PlayerUpdatePacketUpdateTemporaryAppearance
            {
                Guid = Guid,
                TemporaryAppearance = TemporaryAppearance
            }, true);
    }

    #endregion

    #region Update

    public void UpdateEveryTick()
    {
        var now = DateTimeOffset.UtcNow;

        TickEffects(now);
        PlayerPet.UpdateEveryTick();
        SendVisiblePositions();

        while (true)
        {
            (ISerializablePacket Packet, PacketSendType SendType) due;

            lock (_delayedPackets)
            {
                if (!_delayedPackets.TryPeek(out _, out var sendAt) || sendAt > now)
                    break; // none or none ready

                due = _delayedPackets.Dequeue();
            }

            switch (due.SendType)
            {
                case PacketSendType.All:
                    SendTunneledToVisible(due.Packet);
                    break;
                case PacketSendType.AllAndSelf:
                    SendTunneledToVisible(due.Packet, sendToSelf: true);
                    break;
                case PacketSendType.SelfOnly:
                    SendTunneled(due.Packet);
                    break;
            }
        }

        foreach (var (key, scheduled) in _delayedSlotPackets)
        {
            if (scheduled.SendAt > now)
                continue;

            if (_delayedSlotPackets.TryRemove(key, out var removed))
                SendTunneled(removed.Packet);
        }
    }

    public ProjectileHelper Projectiles { get; } = new();
    public int NpcAbilityEffectId { get; set; }

    public NpcAbilityDefinition? NpcAbility { get; set; }
    public DateTimeOffset NpcAbilityExpiresAt { get; set; }
    public DateTimeOffset NpcAbilityNextCastAt { get; set; }
    public int NpcAbilityProfileId { get; set; }

    public void UpdateEverySecond()
    {
        SnowballInteraction.Update(this);
        UpdatePortrait();
    }

    public void RelayPosition(PlayerUpdatePacketUpdatePosition playerUpdatePacketUpdatePosition)
    {
        var data = SerializeTunneled(playerUpdatePacketUpdatePosition);

        LatestPosition = new RelayedPosition(data, playerUpdatePacketUpdatePosition.State, Interlocked.Increment(ref _positionVersion));
    }

    private void SendVisiblePositions()
    {
        if (VisiblePlayers.IsEmpty)
            return;

        var now = Environment.TickCount64;
        var crowded = VisiblePlayers.Count >= CrowdedVisiblePlayerCount;
        List<byte[]>? positionData = null;

        foreach (var (guid, visiblePlayer) in VisiblePlayers)
            QueuePosition(guid, visiblePlayer.LatestPosition, now, crowded, ref positionData);

        if (positionData is not null)
            _connection.Send(UdpChannel.Reliable1, positionData);
    }

    private void QueuePosition(ulong guid, RelayedPosition? latestPosition, long now, bool crowded, ref List<byte[]>? positionData)
    {
        if (latestPosition is null)
            return;

        _sentPositions.TryGetValue(guid, out var sentPosition);

        if (latestPosition.Version == sentPosition.Version)
            return;

        if (latestPosition.State == sentPosition.State && now < sentPosition.NextDueAt)
            return;

        _sentPositions[guid] = (latestPosition.Version, latestPosition.State, crowded ? now + CrowdedRelayIntervalMs : now);

        (positionData ??= []).Add(latestPosition.Data);
    }

    private void UpdatePortrait()
    {
        if (_pendingPortraitCrc is null)
            return;

        if (DateTimeOffset.UtcNow >= _portraitUpdateExpiresAt)
        {
            _pendingPortraitCrc = null;
            return;
        }

        try
        {
            var path = Path.Combine(PortraitStorage.GetCharacterDirectory(Guid), "portrait.crc");

            if (!File.Exists(path))
                return;

            var data = File.ReadAllBytes(path);

            if (data.Length != sizeof(uint) || BitConverter.ToUInt32(data) != _pendingPortraitCrc)
                return;

            var packetPlayerImageDataHeadshot = GetPortraitPacket(PortraitProvider.Headshot);
            var packetPlayerImageDataPortrait = GetPortraitPacket(PortraitProvider.Portrait);

            if (packetPlayerImageDataHeadshot is null || packetPlayerImageDataPortrait is null)
                return;

            SendTunneledToVisible(packetPlayerImageDataHeadshot, true);
            SendTunneledToVisible(packetPlayerImageDataPortrait, true);
            _portraitCrc = _pendingPortraitCrc;
            _pendingPortraitCrc = null;
        }
        catch (IOException)
        {
            // Retry while the upload is being written.
        }
    }

    public void UpdatePortrait(uint portraitCrc)
    {
        if (_portraitCrc == portraitCrc)
            return;

        _pendingPortraitCrc = portraitCrc;
        _portraitUpdateExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
    }

    public PacketPlayerImageData? GetPortraitPacket(PortraitProvider provider)
    {
        var fileName = provider switch
        {
            PortraitProvider.Headshot => "headshot.png",
            PortraitProvider.Portrait => "portrait.png",
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        var path = Path.Combine(PortraitStorage.GetCharacterDirectory(Guid), fileName);

        if (!File.Exists(path))
            return null;

        return new PacketPlayerImageData
        {
            Guid = Guid,
            Provider = provider.ToString(),
            Portrait =
            {
                Unknown2 = 1,
                Guid = Guid,
                ModelId = Model,
                Attachments = GetAttachments(),
                Head = Head,
                Hair = Hair,
                SkinTone = SkinTone,
                FacePaint = FacePaint,
                ModelCustomization = ModelCustomization,
                HairColor = HairColor,
                EyeColor = EyeColor,
                HeadId = HeadId,
                HairId = HairId,
                SkinToneId = SkinToneId,
                FacePaintId = FacePaintId,
                Provider = provider.ToString()
            },
            PngPayload = File.ReadAllBytes(path)
        };
    }

    // The client animates the cooldown sweep itself from TotalRefreshTime -
    // no per-second resend needed for that. But it does NOT re-enable the slot for input on its own once
    // the sweep finishes ("sweep animates but after the sweep I cannot use the ability again") - that
    // needs one explicit packet once the cooldown is actually over. So: one packet now, one packet
    // scheduled for later - not the old repeating-every-second loop, and not silence either.
    public void StartActionBarCooldown(int actionBarId, int slotIndex, int iconId, int nameId, int count, int cooldownMs, int iconTintId = 0, int elapsedMs = 0)
    {
        var remainingMs = Math.Max(0, cooldownMs - elapsedMs);

        SendTunneled(BuildActionBarSlotPacket(actionBarId, slotIndex, iconId, iconTintId, nameId, count, cooldownMs, enabled: false, elapsed: elapsedMs));
        ScheduleSlotPacket(actionBarId, slotIndex, BuildActionBarSlotPacket(actionBarId, slotIndex, iconId, iconTintId, nameId, count, cooldownMs, enabled: true, elapsed: cooldownMs), remainingMs);
    }

    private static ClientUpdatePacketUpdateActionBarSlot BuildActionBarSlotPacket(int actionBarId, int slotIndex, int iconId, int iconTintId, int nameId, int count, int cooldownMs, bool enabled, int elapsed)
    {
        var clientUpdatePacketUpdateActionBarSlot = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = actionBarId, Slot = slotIndex } };
        clientUpdatePacketUpdateActionBarSlot.Slot.IsEmpty = false;
        clientUpdatePacketUpdateActionBarSlot.Slot.IconId = iconId;
        clientUpdatePacketUpdateActionBarSlot.Slot.IconTintId = iconTintId;
        clientUpdatePacketUpdateActionBarSlot.Slot.NameId = nameId;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown5 = ActionBarSlotUnknown5;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown6 = ActionBarSlotUnknown6;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown7 = ActionBarSlotUnknown7;
        clientUpdatePacketUpdateActionBarSlot.Slot.Enabled = enabled;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown10 = elapsed;
        clientUpdatePacketUpdateActionBarSlot.Slot.TotalRefreshTime = cooldownMs;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown12 = elapsed;
        clientUpdatePacketUpdateActionBarSlot.Slot.Quantity = count;
        clientUpdatePacketUpdateActionBarSlot.Slot.ForceDismount = true;
        clientUpdatePacketUpdateActionBarSlot.Slot.Unknown15 = elapsed;
        return clientUpdatePacketUpdateActionBarSlot;
    }

    public void UpdatePosition(Vector4 position, Quaternion rotation, bool updateZoneArea = true)
    {
        Position = position;
        Rotation = rotation;

        Mount?.UpdatePosition(position, rotation, updateZoneArea);

        if (Visible)
        {
            UpdateZoneTile();

            if (updateZoneArea)
                UpdateZoneArea();
        }
    }

    // Nearest other player in the zone within range, excluding excludeGuid if given.
    public Player? FindNearestPlayer(float range, ulong excludeGuid = 0)
    {
        Player? nearest = null;
        var nearestDistance = range * range;

        foreach (var candidate in Zone.Players)
        {
            if (candidate.Guid == Guid || candidate.Guid == excludeGuid)
                continue;

            var distance = candidate.Position.ToVector3().DistanceSquaredXZ(Position.ToVector3());

            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    public Vector3 GetFacingDirection()
    {
        var direction = Rotation.GetFacing();
        var lengthSquared = direction.LengthSquared();

        return float.IsFinite(lengthSquared) && lengthSquared > 0
            ? Vector3.Normalize(direction) : Vector3.UnitZ;
    }

    public IEntity? FindTarget(ulong guid, float range, ulong excludeGuid = 0)
    {
        if (!float.IsFinite(range) || range < 0)
            return null;

        if (guid == 0 || guid == Guid)
            return FindNearestPlayer(range, excludeGuid) ?? FindNearestPlayer(range);

        IEntity? target = VisiblePlayers.TryGetValue(guid, out var player) ? player :
            VisibleNpcs.TryGetValue(guid, out var npc) ? npc : null;
        if (target is null || !ReferenceEquals(target.Zone, Zone))
            return null;

        var distance = Vector3.DistanceSquared(Position.ToVector3(),
            target.Position.ToVector3());

        return float.IsFinite(distance) && distance <= range * range ? target : null;
    }

    private void UpdateZoneTile()
    {
        var newZoneTile = Zone.GetTileFromPosition(Position);

        if (newZoneTile == ZoneTile)
            return;

        Zone.UpdateEntityZoneTile(this, ZoneTile, newZoneTile);

        ZoneTile = newZoneTile;
    }

    public bool TeleportToZone(IZone destinationZone, Vector4 position, Quaternion rotation)
    {
        if (Zone == destinationZone)
            return true;

        if (Zone is WorldZone)
        {
            StartingZonePosition = Position;
            StartingZoneRotation = Rotation;
        }

        if (!_zoneManager.TryMovePlayerToZone(destinationZone.DefinitionId, destinationZone.OwnerId, this, position, rotation, out var zone))
            return false;

        if (_appearanceEffectId != 0 && _effects.TryGetValue(_appearanceEffectId, out var appearanceEffect) && appearanceEffect.ExpiresAt is null)
            RemoveTemporaryAppearance();

        if (Mount is not null && !Mount.TeleportToZone(zone, position, rotation))
            Dismount();

        var packetClientBeginZoning = new PacketClientBeginZoning
        {
            Name = Zone.Name,
            Position = position,
            Rotation = rotation,
            Sky = Zone.Sky,
            Id = Zone.Id,
            GeometryId = 214,
            OverrideUpdateRadius = true
        };

        SendTunneled(packetClientBeginZoning);

        return true;
    }

    private void UpdateZoneArea()
    {
        if (Zone is not WorldZone worldZone || worldZone.Name != "FabledRealms")
            return;

        var zoneAreaId = worldZone.GetZoneAreaId(Position);

        if (ZoneAreaId == zoneAreaId)
            return;

        ZoneAreaId = zoneAreaId;

        var packetPOIChangeMessage = new PacketPOIChangeMessage
        {
            ZoneId = zoneAreaId
        };

        SendTunneled(packetPOIChangeMessage);
    }

    public void SetMountMovementSpeed(float? mountMovementSpeed)
    {
        _mountMovementSpeed = mountMovementSpeed;

        RefreshMovementSpeed();
    }

    private void RefreshMovementSpeed()
    {
        var maxMovementSpeed = _mountMovementSpeed ?? OnFootMovementSpeed + _effects.Values.Sum(effect => effect.MovementSpeedBonus);

        UpdateCharacterStats(CharacterStats.MaxMovementSpeed.Set(maxMovementSpeed));
    }

    public void UpdateCharacterStats(params CharacterStat[] characterStats)
    {
        var clientUpdatePacketUpdateStat = new ClientUpdatePacketUpdateStat
        {
            Guid = Guid
        };

        clientUpdatePacketUpdateStat.Stats.AddRange(characterStats);

        SendTunneled(clientUpdatePacketUpdateStat);

        foreach (var characterStat in characterStats)
        {
            Stats[characterStat.Id] = characterStat;

            if (characterStat.Id == CharacterStatId.MaxMovementSpeed)
            {
                var playerUpdatePacketExpectedSpeed = new PlayerUpdatePacketExpectedSpeed
                {
                    Guid = Guid,
                    ExpectedSpeed = characterStat.Float
                };

                SendTunneledToVisible(playerUpdatePacketExpectedSpeed);
            }
        }
    }

    #endregion

    #region Events

    public void OnAddVisibleNpcs(params IEnumerable<Npc> npcs)
    {
        var visibleNpcs = npcs.Where(npc => npc.IsVisibleTo(this)).ToArray();

        foreach (var npc in visibleNpcs)
        {
            if (npc is Mount)
                continue;

            var playerUpdatePacketAddNpc = npc.GetAddNpcPacket();

            if (npc is PetTreasureChest petTreasureChest && !petTreasureChest.IsOwner(this))
                playerUpdatePacketAddNpc.IsInteractable = false;

            var questImageId = GetNotificationImageId(npc);
            if (questImageId != 0)
                playerUpdatePacketAddNpc.NotificationImageSetId = questImageId;

            SendTunneled(playerUpdatePacketAddNpc);
            SendTunneled(new PlayerUpdatePacketUpdateCharacterState { Guid = npc.Guid, State = IdleCharacterState });
        }

        var playerUpdatePacketNpcRelevance = InteractionMenuHelper.GetNpcRelevancePacket(
            visibleNpcs.Where(npc => npc is not PetTreasureChest petTreasureChest || petTreasureChest.IsOwner(this)));
        if (playerUpdatePacketNpcRelevance.Entries.Count > 0)
            SendTunneled(playerUpdatePacketNpcRelevance);

        var playerUpdatePacketAddNotifications = new PlayerUpdatePacketAddNotifications();

        foreach (var npc in visibleNpcs)
        {
            playerUpdatePacketAddNotifications.Notifications.AddRange(npc.Notifications);

            var questImageId = GetNotificationImageId(npc);
            if (questImageId != 0)
            {
                playerUpdatePacketAddNotifications.Notifications.Add(GetQuestNotification(npc, questImageId));
            }
            else if (npc.Notification is not null)
            {
                playerUpdatePacketAddNotifications.Notifications.Add(npc.Notification);
            }
        }

        if (playerUpdatePacketAddNotifications.Notifications.Count > 0)
            SendTunneled(playerUpdatePacketAddNotifications);

        foreach (var npc in visibleNpcs)
            VisibleNpcs.TryAdd(npc.Guid, npc);
    }

    public int GetNotificationImageId(Npc npc)
    {
        var quests = _resourceManager.Quests;

        if (quests.ByGiver.TryGetValue(npc.DefinitionId, out var giverQuestIds))
        {
            foreach (var questId in giverQuestIds)
            {
                if (quests.TryGet(questId, out var quest) && quest.IsOfferableFor(Quests))
                    return quest.NotificationAvailable;
            }
        }

        if (quests.ByTarget.TryGetValue(npc.DefinitionId, out var targetQuestIds))
        {
            foreach (var questId in targetQuestIds)
            {
                if (Quests.TryGetValue(questId, out var completed) && !completed && quests.TryGet(questId, out var quest))
                    return quest.NotificationActive;
            }
        }

        return npc.Notification?.IconState ?? 0;
    }

    public static NotificationInfo GetQuestNotification(Npc npc, int imageId) => new()
    {
        Guid = npc.Guid,
        IsCompact = false,
        IconState = imageId,
        NameId = npc.NameId,
        ReferenceId = npc.SubTextNameId,
        Enabled = false
    };

    public void OnAddVisiblePlayers(params IEnumerable<Player> players)
    {
        foreach (var player in players)
        {
            if (player.Mount is not null)
            {
                var addPc = player.GetAddPcPacket();
                addPc.MountGuid = 0;
                addPc.MountSeat = -1;
                addPc.MountQueuePosition = -1;
                addPc.NameVerticalOffset = 0;

                SendTunneled(addPc);
                SendTunneled(player.Mount.GetAddNpcPacket());
                SendTunneled(player.Mount.GetMountResponsePacket());
            }
            else
                SendTunneled(player.GetAddPcPacket());

            if (player.Scale != DefaultScale)
                SendTunneled(new PlayerUpdatePacketUpdateScale { Guid = player.Guid, Scale = player.Scale });

            SendTunneled(new PlayerUpdatePacketUpdateCharacterState { Guid = player.Guid, State = IdleCharacterState });

            SendTunneled(player.GetWeaponFlairOverridePacket());

            foreach (var effect in player._effects.Values.Where(effect => effect.WorldEffectStarted))
                SendTunneled(player.GetAddEffectTagCompositeEffectPacket(effect));

            if (player.Mount is null && player.BoomboxDanceIsStanding && player.BoomboxDanceAnimation != 0)
                SendTunneled(new PlayerUpdatePacketSetAnimation
                {
                    Guid = player.Guid,
                    AnimationId = player.BoomboxDanceAnimation,
                    Flags = 1
                });
        }

        foreach (var player in players)
            VisiblePlayers.TryAdd(player.Guid, player);
    }

    public void OnRemoveVisibleNpcs(params IEnumerable<Npc> npcs)
    {
        PlayerUpdatePacketRemoveNotifications? playerUpdatePacketRemoveNotifications = null;

        foreach (var npc in npcs)
        {
            foreach (var notification in npc.Notifications)
                (playerUpdatePacketRemoveNotifications ??= new()).Notifications.Add(GetRemoveNotificationInfo(notification));

            if (npc.Notification is not null)
                (playerUpdatePacketRemoveNotifications ??= new()).Notifications.Add(GetRemoveNotificationInfo(npc.Notification));
        }

        if (playerUpdatePacketRemoveNotifications is not null)
            SendTunneled(playerUpdatePacketRemoveNotifications);

        foreach (var npc in npcs)
        {
            if (npc is Mount)
                continue;

            SendTunneled(new PlayerUpdatePacketRemovePlayer { Guid = npc.Guid });
        }

        foreach (var npc in npcs)
            VisibleNpcs.TryRemove(npc.Guid, out _);
    }

    private static RemoveNotificationInfo GetRemoveNotificationInfo(NotificationInfo notification) => new()
    {
        Guid = notification.Guid,
        ThoughtBubbleIconId = notification.Unknown,
        CompositeEffectId = notification.Unknown2
    };

    public void OnRemoveVisibleNpcGracefully(Npc npc, bool animate)
    {
        if (npc is Mount)
            return;

        SendTunneled(EntityHelper.GetRemovePlayerGracefullyPacket(npc.Guid, animate));

        VisibleNpcs.TryRemove(npc.Guid, out _);
    }

    public void OnRemoveVisiblePlayers(params IEnumerable<Player> players)
    {
        foreach (var player in players)
        {
            SendTunneled(new PlayerUpdatePacketRemovePlayer { Guid = player.Guid });

            if (player.Mount is not null)
                SendTunneled(new PlayerUpdatePacketRemovePlayer { Guid = player.Mount.Guid });
        }

        foreach (var player in players)
        {
            VisiblePlayers.TryRemove(player.Guid, out _);
            _sentPositions.TryRemove(player.Guid, out _);
        }
    }

    public void OnInteract(Player player)
    {
        var commandPacketInteractionList = new CommandPacketInteractionList();

        commandPacketInteractionList.List.Guid = Guid;

        commandPacketInteractionList.List.Interactions.Add(InspectInteraction.Data);

        if (Friends.Any(x => x.Guid == player.Guid))
        {
            commandPacketInteractionList.List.Interactions.Add(RemoveFriendInteraction.Data);
        }
        else
        {
            commandPacketInteractionList.List.Interactions.Add(AddFriendInteraction.Data);
        }

        if (player.Ignores.Any(x => x.Guid == Guid))
        {
            commandPacketInteractionList.List.Interactions.Add(StopIgnoringInteraction.Data);
        }
        else if (!Friends.Any(x => x.Guid == player.Guid))
        {
            commandPacketInteractionList.List.Interactions.Add(IgnoreInteraction.Data);
        }

        if (GuildData is null && GuildInviteInteraction.CanInvite(player))
            commandPacketInteractionList.List.Interactions.Add(GuildInviteInteraction.Data);

        player.SendTunneled(commandPacketInteractionList);
    }

    #endregion

    public ClientItem? GetEquippedItem(int slot) =>
        ActiveProfile.Items.TryGetValue(slot, out var profileItem)
            ? Items.FirstOrDefault(item => item.Id == profileItem.Id)
            : null;

    public int GetWieldType()
    {
        foreach (var equipmentSlot in _resourceManager.EquipmentSlots.ByAnimationPriority)
        {
            if (GetEquippedItem(equipmentSlot.Id) is not { } item
                || !_resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var definition)
                || definition.Class == 0)
                continue;

            if (!_resourceManager.ItemClasses.TryGetValue(definition.Class, out var itemClass))
                return 0;

            if (itemClass.WieldType != 0)
                return itemClass.WieldType;
        }

        return 0;
    }

    public int GetEquippedCompositeEffectId(int slot)
    {
        var item = GetEquippedItem(slot);
        return item is not null && _resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var definition)
            ? Math.Max(0, definition.CompositeEffectId)
            : 0;
    }

    public PlayerUpdatePacketSlotCompositeEffectOverride GetSlotCompositeEffectOverridePacket(int slot, int compositeEffect) => new()
    {
        Guid = Guid,
        Slot = slot,
        CompositeEffect = Math.Max(0, compositeEffect)
    };

    public PlayerUpdatePacketSlotCompositeEffectOverride GetWeaponFlairOverridePacket() =>
        GetSlotCompositeEffectOverridePacket(WeaponSlot, GetEquippedCompositeEffectId(FlairShardSlot));

    public void ClearWeaponFlairOverride() =>
        SendTunneledToVisible(GetSlotCompositeEffectOverridePacket(WeaponSlot, 0), true);

    public void RefreshWeaponFlair(int profileId, int changedSlot)
    {
        if (profileId != ActiveProfileId || (changedSlot != WeaponSlot && changedSlot != FlairShardSlot))
            return;

        // Job switches reuse the cached attachment effect until the new weapon appears.
        if (GetEquippedItem(WeaponSlot) is { } item
            && _resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var definition)
            && GetAttachment(WeaponSlot) is { } attachment)
        {
            SendTunneledToVisible(new PlayerUpdatePacketEquipItemChange
            {
                Guid = Guid,
                Id = item.Id,
                Attachment = attachment,
                ProfileId = ActiveProfileId,
                ItemClassId = definition.Class,
                WieldType = GetWieldType()
            }, true);
        }

        SendTunneledToVisible(GetWeaponFlairOverridePacket(), true);
    }

    public List<CharacterAttachmentData> GetAttachments()
    {
        var list = new List<CharacterAttachmentData>();

        foreach (var profileItem in ActiveProfile.Items)
        {
            var attachment = GetAttachment(profileItem.Key);

            if (attachment is null)
                continue;

            list.Add(attachment);
        }

        return list;
    }

    public CharacterAttachmentData? GetAttachment(int slot)
    {
        var clientItem = GetEquippedItem(slot);

        if (clientItem is null)
            return null;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
            return null;

        var compositeEffectId = clientItemDefinition.CompositeEffectId;

        if (slot == WeaponSlot)
        {
            var flairEffectId = GetEquippedCompositeEffectId(FlairShardSlot);

            if (flairEffectId > 0)
                compositeEffectId = flairEffectId;
        }

        return new CharacterAttachmentData
        {
            ModelName = clientItemDefinition.ModelName,
            TextureAlias = clientItemDefinition.TextureAlias,
            TintAlias = clientItemDefinition.TintAlias,
            TintId = clientItem.Tint == 0 ? clientItemDefinition.Icon.TintId : clientItem.Tint,
            CompositeEffectId = compositeEffectId,
            Slot = clientItemDefinition.Slot
        };
    }

    public PlayerUpdatePacketAddPc GetAddPcPacket()
    {
        bool isReferee = IsMod || IsAdmin;
        var playerUpdatePacketAddPc = new PlayerUpdatePacketAddPc
        {
            Guid = Guid,

            Name = Name,

            Model = Model,

            ChatBubbleForegroundColor = ChatBubbleForegroundColor,
            ChatBubbleBackgroundColor = ChatBubbleBackgroundColor,
            ChatBubbleSize = ChatBubbleSize,

            Position = Position,
            Rotation = Rotation,

            Attachments = GetAttachments(),

            Head = Head,
            Hair = Hair,

            HairColor = HairColor,
            EyeColor = EyeColor,

            SkinTone = SkinTone,

            FacePaint = FacePaint,
            ModelCustomization = ModelCustomization,

            MaxMovementSpeed = Stats[CharacterStatId.MaxMovementSpeed],

            IsUnderage = Age < 18,
            IsMember = MembershipStatus != 0,
            IsReferee = isReferee,

            TemporaryAppearance = TemporaryAppearance,

            ActiveProfileId = ActiveProfileId,

            WieldType = GetWieldType(),

            MountQueuePosition = -1,
            MountSeat = -1,
        };

        var activeTitle = Titles.FirstOrDefault(x => x.Id == ActiveTitle);

        if (activeTitle is not null)
            playerUpdatePacketAddPc.Title = activeTitle;

        if (Mount is not null)
        {
            playerUpdatePacketAddPc.MountGuid = Mount.Guid;
            playerUpdatePacketAddPc.MountSeat = Mount.Seat;
            playerUpdatePacketAddPc.MountQueuePosition = Mount.QueuePosition;

            playerUpdatePacketAddPc.NameVerticalOffset = Mount.Definition.NameVerticalOffset;
        }

        if (GuildData is not null)
            playerUpdatePacketAddPc.Guilds.Add(0, GuildData.Guid);

        return playerUpdatePacketAddPc;
    }

    public const int ChangeFormBuffIconId = 3843;

    public void ApplyTemporaryAppearance(int modelId, int durationMs, int effectId = 0, int buffNameId = 0)
    {
        if (_appearanceEffectId != 0)
            RemoveEffect(_appearanceEffectId);

        _appearanceEffectId = AddEffect(new PlayerEffect
        {
            ExpiresAt = durationMs > 0 ? DateTimeOffset.UtcNow.AddMilliseconds(durationMs) : null,
            AppearanceModelId = modelId,
            AppearancePoofEffectId = effectId,
            BuffIconId = buffNameId != 0 ? ChangeFormBuffIconId : 0,
            BuffNameId = buffNameId,
            OnRemoved = () => _appearanceEffectId = 0
        });
    }

    public void RemoveTemporaryAppearance()
    {
        if (_appearanceEffectId != 0)
            RemoveEffect(_appearanceEffectId);
    }

    public void SetScale(float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0)
            return;

        Scale = scale;

        SendTunneledToVisible(new PlayerUpdatePacketUpdateScale { Guid = Guid, Scale = scale }, true);
    }

    public void ApplyTemporaryScale(float scale, int durationMs, int buffNameId = 0)
    {
        if (_scaleEffectId != 0)
            RemoveEffect(_scaleEffectId);

        _scaleEffectId = AddEffect(new PlayerEffect
        {
            ExpiresAt = durationMs > 0 ? DateTimeOffset.UtcNow.AddMilliseconds(durationMs) : null,
            Scale = scale,
            BuffIconId = buffNameId != 0 ? ChangeFormBuffIconId : 0,
            BuffNameId = buffNameId,
            OnRemoved = () => _scaleEffectId = 0
        });
    }

    public void RemoveTemporaryScale()
    {
        if (_scaleEffectId != 0)
            RemoveEffect(_scaleEffectId);
    }

    #region Effects

    // A tracked, timed effect on the player - a transformation, a food effect aura, or both a
    // world-visible composite effect and a buff bar icon at once. One tick loop expires them,
    // one pair of methods applies/unapplies whatever packets each of those parts needs, so adding
    // a new kind of effect doesn't mean adding another set of ad hoc fields and conditions.
    private readonly ConcurrentDictionary<int, PlayerEffect> _effects = new();
    private int _appearanceEffectId;
    private int _scaleEffectId;
    private float? _mountMovementSpeed;

    public int AddEffect(PlayerEffect effect)
    {
        effect.Id = EffectTagIdGenerator.Next();
        _effects[effect.Id] = effect;

        ApplyEffect(effect);

        return effect.Id;
    }

    public void RemoveEffect(int id)
    {
        if (!_effects.TryRemove(id, out var effect))
            return;

        UnapplyEffect(effect);
        effect.OnRemoved?.Invoke();
    }

    private void TickEffects(DateTimeOffset now)
    {
        List<PlayerEffect> starting = [];
        List<int> expired = [];

        foreach (var effect in _effects.Values)
        {
            if (!effect.WorldEffectStarted && effect.WorldEffectId != 0 &&
                (effect.WorldEffectStartsAt is null || effect.WorldEffectStartsAt <= now))
                starting.Add(effect);

            if (effect.ExpiresAt is { } expiresAt && expiresAt <= now)
                expired.Add(effect.Id);
        }

        foreach (var effect in starting)
            StartWorldEffect(effect);

        foreach (var id in expired)
            RemoveEffect(id);
    }

    private void ApplyEffect(PlayerEffect effect)
    {
        if (effect.AppearanceModelId != 0)
        {
            TemporaryAppearance = effect.AppearanceModelId;

            if (effect.AppearancePoofEffectId != 0)
                SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect { Guid = Guid, CompositeEffectId = effect.AppearancePoofEffectId, Position = Position, Clear = false }, true);

            SendTunneledToVisible(new PlayerUpdatePacketUpdateTemporaryAppearance { Guid = Guid, TemporaryAppearance = effect.AppearanceModelId }, true);

            ResyncWorldEffects();
        }

        if (effect.Scale != 0)
            SetScale(effect.Scale);

        if (effect.WorldEffectId != 0 && (effect.WorldEffectStartsAt is null || effect.WorldEffectStartsAt <= DateTimeOffset.UtcNow))
            StartWorldEffect(effect);

        if (effect.BuffIconId != 0)
            SendEffectTag(effect);

        if (effect.MovementSpeedBonus != 0)
            RefreshMovementSpeed();
    }

    public bool RefreshEffect(int id, DateTimeOffset expiresAt)
    {
        if (!_effects.TryGetValue(id, out var effect))
            return false;

        effect.ExpiresAt = expiresAt;

        if (effect.BuffIconId != 0)
            SendEffectTag(effect);

        return true;
    }

    private void SendEffectTag(PlayerEffect effect)
    {
        var durationSeconds = effect.ExpiresAt is { } expiresAt
            ? Math.Max(1, (int)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds)
            : 0;

        SendTunneled(new ClientUpdatePacketAddEffectTag
        {
            TagId = effect.Id,
            EffectTag = new EffectTag
            {
                InstanceId = effect.Id,
                DefinitionId = effect.BuffDefinitionId,
                Duration = durationSeconds,
                StartTime = 0,
                StopTime = (uint)-durationSeconds
            },
            IconId = effect.BuffIconId,
            NameId = effect.BuffNameId
        });
    }

    private void UnapplyEffect(PlayerEffect effect)
    {
        if (effect.AppearanceModelId != 0)
        {
            TemporaryAppearance = 0;
            SendTunneledToVisible(new PlayerUpdatePacketRemoveTemporaryAppearance { Guid = Guid }, true);
            ResyncWorldEffects();
        }

        if (effect.Scale != 0)
            SetScale(_effects.Values.FirstOrDefault(e => e.Id != effect.Id && e.Scale != 0)?.Scale ?? DefaultScale);

        if (effect.WorldEffectStarted)
        {
            SendTunneledToVisible(new PlayerUpdatePacketRemoveEffectTagCompositeEffect { Guid = Guid, TagId = effect.WorldTagId }, true);
            effect.WorldEffectStarted = false;
        }

        if (effect.BuffIconId != 0)
            SendTunneled(new ClientUpdatePacketRemoveEffectTag { TagId = effect.Id });

        if (effect.MovementSpeedBonus != 0)
            RefreshMovementSpeed();
    }

    private void StartWorldEffect(PlayerEffect effect)
    {
        effect.WorldEffectStarted = true;
        effect.WorldTagId = EffectTagIdGenerator.Next();

        SendTunneledToVisible(GetAddEffectTagCompositeEffectPacket(effect), true);
    }

    private PlayerUpdatePacketAddEffectTagCompositeEffect GetAddEffectTagCompositeEffectPacket(PlayerEffect effect) => new()
    {
        Guid = Guid,
        TagId = effect.WorldTagId,
        CompositeEffectId = effect.WorldEffectId,
        SourceGuid = effect.SourceGuid != 0 ? effect.SourceGuid : Guid
    };

    // A temporary appearance change is unreliable about which world-visible effect tags survive
    // it - sometimes a tag keeps playing across it, sometimes it doesn't, and the client also
    // ignores a repeat of a tag id it's already seen for this actor. Rather than guess which case
    // this is, explicitly drop and re-add every effect that's supposed to be showing one, with a
    // fresh id, on every appearance change in either direction.
    private void ResyncWorldEffects()
    {
        var active = _effects.Values.Where(e => e.WorldEffectStarted).ToList();

        foreach (var effect in active)
        {
            SendTunneledToVisible(new PlayerUpdatePacketRemoveEffectTagCompositeEffect { Guid = Guid, TagId = effect.WorldTagId }, true);
            StartWorldEffect(effect);
        }
    }

    #endregion

    #region Combat


    private int _energy = 100;

    public int Energy
    {
        get => _energy;
        private set
        {
            _energy = Math.Min(value, MaxEnergy);

            SendTunneled(new ClientUpdatePacketMana
            {
                CurrentMana = _energy,
                MaxMana = MaxEnergy
            });
        }
    }

    private int MaxEnergy { get; set; } = 100;

    public int GetEquippedWeaponDefinitionId() => GetEquippedItem(WeaponSlot)?.Definition ?? 0;

    public bool SendToolbar()
    {
        if (!_resourceManager.CombatJobs.TryGetValue(ActiveProfileId, out var kit))
        {
            SendTunneled(new AbilityPacketSetDefinition { ProfileId = ActiveProfileId });
            return false;
        }

        var abilityPacketSetDefinition = new AbilityPacketSetDefinition { ProfileId = kit.ProfileId };

        var weaponDefinitionId = GetEquippedWeaponDefinitionId();

        if (_resourceManager.ClientItemDefinitions.TryGetValue(weaponDefinitionId, out var weaponDefinition))
        {
            var (basic, special) = ResolveWeaponAbilities(kit, weaponDefinitionId);

            if (basic is not null)
            {
                abilityPacketSetDefinition.AbilitySet.Abilities[0] = CreateToolbarSlot(kit.BasicSlotDefId, basic.IconId, weaponDefinition.NameId, manaCost: 0);
                SendAbilityDefinition(kit.BasicSlotDefId, basic);
            }

            if (special is not null)
            {
                abilityPacketSetDefinition.AbilitySet.Abilities[1] = CreateToolbarSlot(kit.SpecialSlotDefId, special.IconId, weaponDefinition.NameId, special.EnergyCost);
                SendAbilityDefinition(kit.SpecialSlotDefId, special);
            }
        }

        SendTunneled(abilityPacketSetDefinition);

        MaxEnergy = kit.Energy.Max;
        // Resync energy against the new max.
        Energy = _energy;

        return true;
    }

    private void SendAbilityDefinition(int abilityDefinitionId, AbilityDefinition ability)
    {
        SendTunneled(new AbilityPacketAbilityDefinition
        {
            AbilityId = abilityDefinitionId,
            NameId = ability.NameId,
            DescriptionId = ability.DescriptionId,
            IconId = ability.IconId,
            ManaCost = ability.EnergyCost
        });
    }

    private static Ability CreateToolbarSlot(int abilityDefinitionId, int iconId, int nameId, int manaCost) => new()
    {
        Type = 3,
        InstanceId = abilityDefinitionId,
        ManaCost = manaCost,
        IconId = iconId,
        NameId = nameId,
        Unknown7 = 4,
        Unknown9 = 1,
        AbilityDefinitionId = abilityDefinitionId,
        ForceDismount = true
    };

    private (AbilityDefinition? Basic, AbilityDefinition? Special) ResolveWeaponAbilities(JobKitDefinition kit, int weaponDefinitionId)
    {
        var mapping = kit.Weapons.FirstOrDefault(w => w.WeaponDefIds.Contains(weaponDefinitionId));

        var basicId = mapping?.BasicAbilityId ?? kit.FallbackBasicAbilityId;
        var specialId = mapping?.SpecialAbilityId ?? 0;

        return (
            _resourceManager.CombatAbilities.TryGetValue(basicId, out var basic) ? basic : null,
            _resourceManager.CombatAbilities.TryGetValue(specialId, out var special) ? special : null);
    }

    #endregion

    #region Equatable

    public bool Equals(IEntity? other)
    {
        return Guid == other?.Guid;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is Player other)
            return Equals(other);

        return false;
    }

    public override int GetHashCode()
    {
        return Guid.GetHashCode();
    }

    public static bool operator ==(Player left, Player right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Player left, Player right)
    {
        return !(left == right);
    }

    #endregion

    private void RemoveFromVisibleEntities(bool notifySelf)
    {
        foreach (var visibleNpc in VisibleNpcs)
            visibleNpc.Value.OnRemoveVisiblePlayers([this]);

        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.OnRemoveVisiblePlayers([this]);

        if (notifySelf)
        {
            OnRemoveVisibleNpcs(VisibleNpcs.Values);
            OnRemoveVisiblePlayers(VisiblePlayers.Values);
        }
    }

    public void OnZoneChanged(IZone zone, Vector4 position, Quaternion rotation)
    {
        PlayerPet.RecallPet(remember: zone is not CombatZone);

        RemoveFromVisibleEntities(true);

        ZoneTile.Entities.Remove(Guid, out _);

        Zone = zone;

        ZoneTile = ZoneTile.Empty;

        Visible = false;

        UpdatePosition(position, rotation);
    }

    public void Dispose()
    {
        PlayerPet.Dispose();

        RemoveFromVisibleEntities(false); // no need to notify self since we're DCing

        Mount?.Dispose();
        Mount = null;

        ZoneTile.Entities.Remove(Guid, out _);
        Zone.TryRemovePlayer(Guid);
        _zoneManager.EvictIfEmpty(Zone);
    }

    public sealed record InteractionMenu(ulong Guid, IReadOnlyDictionary<int, Action<Player>> Options);

    public InteractionMenu? OpenInteractionMenu { get; set; }

    private const int NpcInteractionIdBase = 1_000_000;

    public void SendInteractionMenu(Npc npc, IReadOnlyList<NpcInteractionOption> options)
    {
        var interactions = new List<InteractionData>(options.Count);
        var actions = new Dictionary<int, Action<Player>>(options.Count);

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var id = NpcInteractionIdBase + i;

            interactions.Add(new InteractionData
            {
                Id = id,
                IconId = option.IconId,
                ButtonText = option.ButtonTextId,
                TooltipId = option.TooltipId
            });

            actions[id] = option.Invoke;
        }

        OpenInteractionMenu = new InteractionMenu(npc.Guid, actions);

        SendTunneled(InteractionMenuHelper.GetInteractionListPacket(npc.Guid, npc.Name, interactions));
    }
}
