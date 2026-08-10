using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.Collections;
using Sanctuary.Core.IO;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Chat;
using Sanctuary.UdpLibrary;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.Game.Entities;

public sealed class Player : ClientPcData, IEntity
{
    private readonly UdpConnection _connection;
    private readonly IResourceManager _resourceManager;

    public bool Visible { get; set; }

    public IZone Zone { get; set; }
    public ZoneTile ZoneTile { get; private set; } = ZoneTile.Empty;
    public ConcurrentDictionary<ulong, Npc> VisibleNpcs { get; } = [];
    public ConcurrentDictionary<ulong, Player> VisiblePlayers { get; } = [];

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

    public List<FriendData> Friends { get; set; } = [];
    public List<IgnoreData> Ignores { get; set; } = [];

    public ConcurrentSet<ulong> IncomingFriendRequests { get; } = [];
    public ConcurrentSet<ulong> IncomingGuildInvites { get; } = [];

    public ConcurrentDictionary<ChatChannel, bool> ChatChannelStatus { get; set; } = [];

    public int StationCash { get; set; }
    public List<CoinStoreTransactionRecord> CoinStoreTransactions { get; set; } = [];

    public GuildData? GuildData { get; set; }

    public int TimezoneOffset { get; set; }

    public Dictionary<int, Dictionary<int, int>> ActionBarItemGuids { get; set; } = new();

    public int TemporaryAppearance { get; set; }
    public DateTimeOffset? TemporaryAppearanceExpiresAt { get; set; }
    private int _temporaryAppearanceEffectId;

    private readonly ConcurrentQueue<(DateTimeOffset SendAt, ISerializablePacket Packet, bool SendToSelf)> _delayedPackets = new();

    // One scheduled personal-UI packet per action bar slot (the cooldown re-enable) - keyed, not queued,
    // so a slot that gets emptied before its cooldown naturally expires (last item consumed) can cancel
    // its own pending re-enable instead of it firing later and silently un-deleting the slot.
    private readonly ConcurrentDictionary<(int, int), (DateTimeOffset SendAt, ISerializablePacket Packet)> _delayedSlotPackets = new();

    public void ScheduleSlotPacket(int actionBarId, int slotIndex, ISerializablePacket packet, int delayMs)
    {
        _delayedSlotPackets[(actionBarId, slotIndex)] = (DateTimeOffset.UtcNow.AddMilliseconds(delayMs), packet);
    }

    public void CancelScheduledSlotPacket(int actionBarId, int slotIndex) => _delayedSlotPackets.TryRemove((actionBarId, slotIndex), out _);

    public Vector4 StartingZonePosition { get; set; }
    public Quaternion StartingZoneRotation { get; set; }

    public Player(BaseZone zone, UdpConnection connection, IResourceManager resourceManager)
    {
        Zone = zone;

        _connection = connection;
        _resourceManager = resourceManager;
    }

    #region Connection

    public void Send(ISerializablePacket packet)
    {
        var data = packet.Serialize();

        _connection.Send(UdpChannel.Reliable1, data);
    }

    public void SendToVisible(ISerializablePacket packet, bool sendToSelf = false)
    {
        var visiblePlayers = VisiblePlayers.ToFrozenDictionary();

        foreach (var visiblePlayer in visiblePlayers)
            visiblePlayer.Value.Send(packet);

        if (sendToSelf)
            Send(packet);
    }

    public void SendTunneled(ISerializablePacket packet)
    {
        var packetTunneled = new PacketTunneledClientPacket
        {
            Payload = packet.Serialize()
        };

        Send(packetTunneled);
    }

    [Obsolete]
    public void SendTunneled(byte[] buffer)
    {
        var packetTunneled = new PacketTunneledClientPacket
        {
            Payload = buffer
        };

        Send(packetTunneled);
    }

    public void SendTunneledToVisible(ISerializablePacket packet, bool sendToSelf = false)
    {
        var visiblePlayers = VisiblePlayers.ToFrozenDictionary();

        foreach (var visiblePlayer in visiblePlayers)
            visiblePlayer.Value.SendTunneled(packet);

        if (sendToSelf)
            SendTunneled(packet);
    }

    public void SendTunneledToVisibleDelayed(ISerializablePacket packet, int delayMs, bool sendToSelf = false)
    {
        _delayedPackets.Enqueue((DateTimeOffset.UtcNow.AddMilliseconds(delayMs), packet, sendToSelf));
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
            CompositeEffectId = 0
        }, sendToSelf: true);

        UpdateCharacterStats(
            CharacterStats.MaxMovementSpeed.Set(8f),
            CharacterStats.GlideEnabled.Set(0),
            CharacterStats.JumpHeight.Set(0f));

        SendTunneledToVisible(new PlayerUpdatePacketRemovePlayerGracefully
        {
            Guid = Mount.Guid,
            Animate = false,
            Delay = 0,
            EffectDelay = 0,
            CompositeEffectId = 46,
            Duration = 1000
        }, sendToSelf: true);

        Mount.Dispose();
        Mount = null;
    }

    #endregion

    #region Update

    public void UpdateEveryTick()
    {
        if (TemporaryAppearanceExpiresAt.HasValue &&
            TemporaryAppearanceExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            RemoveTemporaryAppearance();
        }

        while (_delayedPackets.TryPeek(out var delayed) && delayed.SendAt <= DateTimeOffset.UtcNow)
        {
            if (_delayedPackets.TryDequeue(out delayed))
                SendTunneledToVisible(delayed.Packet, delayed.SendToSelf);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var (key, scheduled) in _delayedSlotPackets)
        {
            if (scheduled.SendAt > now)
                continue;

            if (_delayedSlotPackets.TryRemove(key, out var removed))
                SendTunneled(removed.Packet);
        }
    }

    public void UpdateEverySecond()
    {
    }

    // Live-confirmed (2026-07-31): the client animates the cooldown sweep itself from TotalRefreshTime -
    // no per-second resend needed for that. But it does NOT re-enable the slot for input on its own once
    // the sweep finishes ("sweep animates but after the sweep I cannot use the ability again") - that
    // needs one explicit packet once the cooldown is actually over. So: one packet now, one packet
    // scheduled for later - not the old repeating-every-second loop, and not silence either.
    public void StartActionBarCooldown(int actionBarId, int slotIndex, int iconId, int nameId, int count, int cooldownMs, int iconTintId = 0)
    {
        SendTunneled(BuildActionBarSlotPacket(actionBarId, slotIndex, iconId, iconTintId, nameId, count, cooldownMs, enabled: false, elapsed: 0));
        ScheduleSlotPacket(actionBarId, slotIndex, BuildActionBarSlotPacket(actionBarId, slotIndex, iconId, iconTintId, nameId, count, cooldownMs, enabled: true, elapsed: cooldownMs), cooldownMs);
    }

    private static ClientUpdatePacketUpdateActionBarSlot BuildActionBarSlotPacket(int actionBarId, int slotIndex, int iconId, int iconTintId, int nameId, int count, int cooldownMs, bool enabled, int elapsed)
    {
        var packet = new ClientUpdatePacketUpdateActionBarSlot { Data = { Id = actionBarId, Slot = slotIndex } };
        packet.Slot.IsEmpty = false;
        packet.Slot.IconId = iconId;
        packet.Slot.IconTintId = iconTintId;
        packet.Slot.NameId = nameId;
        packet.Slot.Unknown5 = 1;
        packet.Slot.Unknown6 = 4;
        packet.Slot.Unknown7 = 15;
        packet.Slot.Enabled = enabled;
        packet.Slot.Unknown10 = elapsed;
        packet.Slot.TotalRefreshTime = cooldownMs;
        packet.Slot.Unknown12 = elapsed;
        packet.Slot.Quantity = count;
        packet.Slot.ForceDismount = true;
        packet.Slot.Unknown15 = elapsed;
        return packet;
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

    private void UpdateZoneTile()
    {
        var newZoneTile = Zone.GetTileFromPosition(Position);

        if (newZoneTile == ZoneTile)
            return;

        Zone.UpdateEntityZoneTile(this, ZoneTile, newZoneTile);

        ZoneTile = newZoneTile;
    }

    public void TeleportToZone(IZone zone, Vector4 position, Quaternion rotation)
    {
        if (Zone == zone)
            return;

        if (Zone is StartingZone)
        {
            StartingZonePosition = Position;
            StartingZoneRotation = Rotation;
        }

        if (Mount is not null)
            Mount.TeleportToZone(zone, position, rotation);

        // Alert/Remove visible entities
        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.OnRemoveVisiblePlayers([this]);

        OnRemoveVisibleNpcs(VisibleNpcs.Values);
        OnRemoveVisiblePlayers(VisiblePlayers.Values);

        ZoneTile.Entities.Remove(Guid, out _);

        Zone.TryRemovePlayer(Guid);

        // Add to new zone/zonetile

        zone.TryAddPlayer(this);

        // Teleport to new zone

        Visible = false;

        Zone = zone;

        ZoneTile = ZoneTile.Empty;

        UpdatePosition(position, rotation);

        var packetClientBeginZoning = new PacketClientBeginZoning
        {
            Name = Zone.Name,
            Position = position,
            Rotation = rotation,
            Sky = "sky_deep_mines.xml",
            Id = Zone.Id,
            GeometryId = 214,
            OverrideUpdateRadius = true
        };

        SendTunneled(packetClientBeginZoning);
    }

    private void UpdateZoneArea()
    {
        if (Zone is not StartingZone startingZone)
            return;

        var zoneAreaId = startingZone.GetZoneAreaId(Position);

        if (ZoneAreaId == zoneAreaId)
            return;

        ZoneAreaId = zoneAreaId;

        var packetPOIChangeMessage = new PacketPOIChangeMessage
        {
            ZoneId = zoneAreaId
        };

        SendTunneled(packetPOIChangeMessage);
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
        foreach (var npc in npcs)
        {
            if (npc is Mount)
                continue;

            SendTunneled(npc.GetAddNpcPacket());
        }

        var playerUpdatePacketNpcRelevance = new PlayerUpdatePacketNpcRelevance();

        foreach (var npc in npcs)
        {
            if (npc.CursorId == 0)
                continue;

            playerUpdatePacketNpcRelevance.Entries.Add(new PlayerUpdatePacketNpcRelevance.Entry
            {
                Guid = npc.Guid,
                HasCursor = true,
                CursorId = npc.CursorId
            });
        }

        if (playerUpdatePacketNpcRelevance.Entries.Count > 0)
            SendTunneled(playerUpdatePacketNpcRelevance);

        var playerUpdatePacketAddNotifications = new PlayerUpdatePacketAddNotifications();

        foreach (var npc in npcs)
        {
            if (npc.Notification is null)
                continue;

            playerUpdatePacketAddNotifications.Notifications.Add(npc.Notification);
        }

        if (playerUpdatePacketAddNotifications.Notifications.Count > 0)
            SendTunneled(playerUpdatePacketAddNotifications);

        foreach (var npc in npcs)
            VisibleNpcs.TryAdd(npc.Guid, npc);
    }

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
        }

        foreach (var player in players)
            VisiblePlayers.TryAdd(player.Guid, player);
    }

    public void OnRemoveVisibleNpcs(params IEnumerable<Npc> npcs)
    {
        foreach (var npc in npcs)
        {
            if (npc is Mount)
                continue;

            SendTunneled(new PlayerUpdatePacketRemovePlayer { Guid = npc.Guid });
        }

        foreach (var npc in npcs)
            VisibleNpcs.TryRemove(npc.Guid, out _);
    }

    public void OnRemoveVisibleNpcGracefully(Npc npc, bool animate, int delay, int effectDelay,
        int compositeEffectId, int duration)
    {
        if (npc is Mount)
            return;

        SendTunneled(new PlayerUpdatePacketRemovePlayerGracefully
        {
            Guid = npc.Guid,
            Animate = animate,
            Delay = delay,
            EffectDelay = effectDelay,
            CompositeEffectId = compositeEffectId,
            Duration = duration
        });

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
            VisiblePlayers.TryRemove(player.Guid, out _);
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
        else
        {
            commandPacketInteractionList.List.Interactions.Add(IgnoreInteraction.Data);
        }

        if (GuildData is null && GuildInviteInteraction.CanInvite(player))
            commandPacketInteractionList.List.Interactions.Add(GuildInviteInteraction.Data);

        player.SendTunneled(commandPacketInteractionList);
    }

    #endregion

    public int GetFlairShardCompositeEffect()
    {
        const int FlairShardSlot = 13;

        if (ActiveProfile.Items.TryGetValue(FlairShardSlot, out var profileItem))
        {
            var clientItem = Items.FirstOrDefault(x => x.Id == profileItem.Id);

            if (clientItem is not null)
            {
                if (_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
                    return clientItemDefinition.CompositeEffectId;
            }
        }

        return 0;
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
        if (!ActiveProfile.Items.TryGetValue(slot, out var profileItem))
            return null;

        var clientItem = Items.FirstOrDefault(x => x.Id == profileItem.Id);

        if (clientItem is null)
            return null;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
            return null;

        var compositeEffectId = clientItemDefinition.CompositeEffectId;

        // Update the Weapon composite effect if we have a Flair Shard equipped.
        if (slot == 7)
        {
            var flairShardcompositeEffectId = GetFlairShardCompositeEffect();

            if (flairShardcompositeEffectId > 0)
                compositeEffectId = flairShardcompositeEffectId;
        }

        return new CharacterAttachmentData
        {
            ModelName = clientItemDefinition.ModelName,
            TextureAlias = clientItemDefinition.TextureAlias,
            TintAlias = clientItemDefinition.TintAlias,
            TintId = clientItem.Tint,
            CompositeEffectId = compositeEffectId,
            Slot = clientItemDefinition.Slot
        };
    }

    public PlayerUpdatePacketAddPc GetAddPcPacket()
    {
        var packet = new PlayerUpdatePacketAddPc
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

            TemporaryAppearance = TemporaryAppearance,

            ActiveProfileId = ActiveProfileId,

            MountQueuePosition = -1,
            MountSeat = -1,
        };

        var activeTitle = Titles.FirstOrDefault(x => x.Id == ActiveTitle);

        if (activeTitle is not null)
            packet.Title = activeTitle;

        if (Mount is not null)
        {
            packet.MountGuid = Mount.Guid;
            packet.MountSeat = Mount.Seat;
            packet.MountQueuePosition = Mount.QueuePosition;

            packet.NameVerticalOffset = Mount.Definition.NameVerticalOffset;
        }

        if (GuildData is not null)
            packet.Guilds.Add(0, GuildData.Guid);

        return packet;
    }

    public void ApplyTemporaryAppearance(int modelId, int durationMs, int effectId = 0)
    {
        TemporaryAppearance = modelId;
        _temporaryAppearanceEffectId = effectId;

        if (durationMs > 0)
            TemporaryAppearanceExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds(durationMs);

        if (effectId != 0)
            SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect { Guid = Guid, CompositeEffectId = effectId, Position = Position, Clear = false }, true);

        SendTunneledToVisible(new PlayerUpdatePacketUpdateTemporaryAppearance { Guid = Guid, TemporaryAppearance = modelId }, true);
    }

    public void RemoveTemporaryAppearance()
    {
        TemporaryAppearance = 0;
        TemporaryAppearanceExpiresAt = null;

        if (_temporaryAppearanceEffectId != 0)
        {
            SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect { Guid = Guid, CompositeEffectId = _temporaryAppearanceEffectId, Position = Position, Clear = false }, true);
            _temporaryAppearanceEffectId = 0;
        }

        SendTunneledToVisible(new PlayerUpdatePacketRemoveTemporaryAppearance { Guid = Guid }, true);
    }

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

    public void Dispose()
    {
        foreach (var visiblePlayer in VisiblePlayers)
            visiblePlayer.Value.OnRemoveVisiblePlayers([this]);

        Mount?.Dispose();
        Mount = null;

        ZoneTile.Entities.Remove(Guid, out _);
        Zone.TryRemovePlayer(Guid);
    }
}
