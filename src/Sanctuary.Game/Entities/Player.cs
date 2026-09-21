using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Sanctuary.Core.Collections;
using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Resources.Definitions.Combat;
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
    private readonly IZoneManager _zoneManager;

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

    public int TemporaryAppearance { get; private set; }

    public ulong LastSillyStringTarget { get; set; }

    public int ActiveFoodEffectId { get; set; }

    private readonly ConcurrentDictionary<int, DateTimeOffset> _itemCooldowns = new();

    public bool IsItemOnCooldown(int itemDefinitionId) =>
        _itemCooldowns.TryGetValue(itemDefinitionId, out var expiresAt) && DateTimeOffset.UtcNow < expiresAt;

    public void StartItemCooldown(int itemDefinitionId, int cooldownMs) =>
        _itemCooldowns[itemDefinitionId] = DateTimeOffset.UtcNow.AddMilliseconds(cooldownMs);

    // For one-off delayed packets unrelated to tracked effects (e.g. a silly string reset
    // animation, a boombox poof) - see PlayerEffect/AddEffect for anything with its own state.
    // Min-heap ordered by send time.
    private readonly PriorityQueue<(ISerializablePacket Packet, bool SendToSelf), DateTimeOffset> _delayedPackets = new();

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

    public Player(BaseZone zone, UdpConnection connection, IResourceManager resourceManager, IZoneManager zoneManager)
    {
        Zone = zone;

        _connection = connection;
        _resourceManager = resourceManager;
        _zoneManager = zoneManager;
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
        lock (_delayedPackets)
        {
            _delayedPackets.Enqueue((packet, sendToSelf), DateTimeOffset.UtcNow.AddMilliseconds(delayMs));
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
            CharacterStats.MaxMovementSpeed.Set(8f),
            CharacterStats.GlideEnabled.Set(0),
            CharacterStats.JumpHeight.Set(0f));

        SendTunneledToVisible(new PlayerUpdatePacketRemovePlayerGracefully
        {
            Guid = Mount.Guid,
            Animate = false,
            Delay = 0,
            EffectDelay = 0,
            CompositeEffectId = 0,
            Duration = 1000
        }, sendToSelf: true);

        Mount.Dispose();
        Mount = null;
    }

    #endregion

    #region Update

    public void UpdateEveryTick()
    {
        var now = DateTimeOffset.UtcNow;

        TickEffects(now);

        while (true)
        {
            (ISerializablePacket Packet, bool SendToSelf) due;

            lock (_delayedPackets)
            {
                if (!_delayedPackets.TryPeek(out _, out var sendAt) || sendAt > now)
                    break; // none or none ready

                due = _delayedPackets.Dequeue();
            }

            SendTunneledToVisible(due.Packet, due.SendToSelf);
        }

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

    // The client animates the cooldown sweep itself from TotalRefreshTime -
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

    // Nearest other player in the zone within range, excluding excludeGuid if given.
    public Player? FindNearestPlayer(float range, ulong excludeGuid = 0)
    {
        Player? nearest = null;
        var nearestDistance = range * range;

        foreach (var candidate in Zone.Players)
        {
            if (candidate.Guid == Guid || candidate.Guid == excludeGuid)
                continue;

            var deltaX = candidate.Position.X - Position.X;
            var deltaZ = candidate.Position.Z - Position.Z;
            var distance = deltaX * deltaX + deltaZ * deltaZ;

            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
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

        if (!_zoneManager.TryMovePlayerToZone(destinationZone.DefinitionId, destinationZone.OwnerId, this, out var zone))
            return false;

        if (Zone is WorldZone)
        {
            StartingZonePosition = Position;
            StartingZoneRotation = Rotation;
        }

        if (Mount is not null && !Mount.TeleportToZone(zone, position, rotation))
            Dismount();

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
        if (Zone is not WorldZone worldZone)
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
        else if (!Friends.Any(x => x.Guid == player.Guid))
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
        bool isReferee = IsMod || IsAdmin;
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
            IsReferee = isReferee,

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

    #region Effects

    // A tracked, timed effect on the player - a transformation, a food effect aura, or both a
    // world-visible composite effect and a buff bar icon at once. One tick loop expires them,
    // one pair of methods applies/unapplies whatever packets each of those parts needs, so adding
    // a new kind of effect doesn't mean adding another set of ad hoc fields and conditions.
    private readonly ConcurrentDictionary<int, PlayerEffect> _effects = new();
    private int _appearanceEffectId;

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

        if (effect.WorldEffectId != 0 && (effect.WorldEffectStartsAt is null || effect.WorldEffectStartsAt <= DateTimeOffset.UtcNow))
            StartWorldEffect(effect);

        if (effect.BuffIconId != 0)
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
                    Duration = durationSeconds,
                    StartTime = 0,
                    StopTime = (uint)-durationSeconds
                },
                IconId = effect.BuffIconId,
                NameId = effect.BuffNameId
            });
        }
    }

    private void UnapplyEffect(PlayerEffect effect)
    {
        if (effect.AppearanceModelId != 0)
        {
            TemporaryAppearance = 0;
            SendTunneledToVisible(new PlayerUpdatePacketRemoveTemporaryAppearance { Guid = Guid }, true);
            ResyncWorldEffects();
        }

        if (effect.WorldEffectStarted)
        {
            SendTunneledToVisible(new PlayerUpdatePacketRemoveEffectTagCompositeEffect { Guid = Guid, TagId = effect.WorldTagId }, true);
            effect.WorldEffectStarted = false;
        }

        if (effect.BuffIconId != 0)
            SendTunneled(new ClientUpdatePacketRemoveEffectTag { TagId = effect.Id });
    }

    private void StartWorldEffect(PlayerEffect effect)
    {
        effect.WorldEffectStarted = true;
        effect.WorldTagId = EffectTagIdGenerator.Next();

        SendTunneledToVisible(new PlayerUpdatePacketAddEffectTagCompositeEffect
        {
            Guid = Guid,
            TagId = effect.WorldTagId,
            CompositeEffectId = effect.WorldEffectId,
            SourceGuid = Guid
        }, true);
    }

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

    private const int PrimaryWeaponSlot = 7;

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

    public int GetEquippedWeaponDefinitionId()
    {
        if (!ActiveProfile.Items.TryGetValue(PrimaryWeaponSlot, out var profileItem))
            return 0;

        var clientItem = Items.FirstOrDefault(x => x.Id == profileItem.Id);

        return clientItem?.Definition ?? 0;
    }

    public bool SendToolbar()
    {
        if (!_resourceManager.CombatJobs.TryGetValue(ActiveProfileId, out var kit))
        {
            SendTunneled(new AbilityPacketSetDefinition { ProfileId = ActiveProfileId });
            return false;
        }

        var setDefinition = new AbilityPacketSetDefinition { ProfileId = kit.ProfileId };

        var weaponDefinitionId = GetEquippedWeaponDefinitionId();

        if (_resourceManager.ClientItemDefinitions.TryGetValue(weaponDefinitionId, out var weaponDefinition))
        {
            var (basic, special) = ResolveWeaponAbilities(kit, weaponDefinitionId);

            if (basic is not null)
            {
                setDefinition.AbilitySet.Abilities[0] = CreateToolbarSlot(kit.BasicSlotDefId, basic.IconId, weaponDefinition.NameId, manaCost: 0);
                SendAbilityDefinition(kit.BasicSlotDefId, basic);
            }

            if (special is not null)
            {
                setDefinition.AbilitySet.Abilities[1] = CreateToolbarSlot(kit.SpecialSlotDefId, special.IconId, weaponDefinition.NameId, special.EnergyCost);
                SendAbilityDefinition(kit.SpecialSlotDefId, special);
            }
        }

        SendTunneled(setDefinition);

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

    public void OnBeforeZoneChange()
    {
        RemoveFromVisibleEntities(true);

        ZoneTile.Entities.Remove(Guid, out _);
    }

    public void Dispose()
    {
        RemoveFromVisibleEntities(false); // no need to notify self since we're DCing

        Mount?.Dispose();
        Mount = null;

        ZoneTile.Entities.Remove(Guid, out _);
        Zone.TryRemovePlayer(Guid);
        _zoneManager.EvictIfEmpty(Zone);
    }
}
