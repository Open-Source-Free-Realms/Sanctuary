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

    // --- Quests ---

    // Database character id, used to persist quest state (DbCharacterQuest).
    public ulong CharacterId { get; set; }

    // The NPC the player most recently interacted with, and when (gates quest offer/turn-in).
    public ulong LastInteractNpcGuid { get; set; }
    public DateTime LastInteractAt { get; set; }

    // When the player last accepted a quest; used to ignore a stray abandon fired right after accept.
    public DateTime LastQuestAcceptedAt { get; set; }

    // QuestId -> completed. Presence in the map means the quest has been accepted.
    public Dictionary<int, bool> Quests { get; } = new();

    // QuestId -> goals completed so far (goals tick off in order).
    public Dictionary<int, int> QuestGoalProgress { get; } = new();

    // QuestId -> collect count for the active Collect goal (in-memory; a relog restarts it).
    public Dictionary<int, int> QuestCollectProgress { get; } = new();

    // Collect pickups this player has already gathered (shared world objects, hidden per-player).
    public HashSet<ulong> CollectedPickups { get; } = new();

    // The quest currently tracked (the objective arrow points at this quest). 0 = none.
    public int ActiveQuestId { get; set; }

    // Quest turn-in finalization, invoked once when the client confirms the end screen.
    public System.Action? PendingQuestEndAction { get; set; }

    // Sends a "+XP" popup for the active profile (visual only - this codebase has no job-leveling
    // system yet, so there's no level bar to actually advance).
    public void AwardXp(int xp)
    {
        SendTunneled(new ClientUpdatePacketUpdateProfileExperience
        {
            ProfileId = ActiveProfileId,
            XpGained = xp,
            TotalXpInLevel = 0,
            CurrentLevel = 0
        });
    }

    public ConcurrentDictionary<ChatChannel, bool> ChatChannelStatus { get; set; } = [];

    public int StationCash { get; set; }
    public List<CoinStoreTransactionRecord> CoinStoreTransactions { get; set; } = [];

    public GuildData? GuildData { get; set; }

    public int TimezoneOffset { get; set; }

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
    }

    public void UpdateEverySecond()
    {
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

            var playerUpdatePacketAddNpc = npc.GetAddNpcPacket();

            // The badge ("!"/"?") is driven by the AddNpc packet's own NotificationImageSetId field, not
            // just the separate NotificationInfo packet below - quest badges are per-player, so this has
            // to be overridden per-recipient here rather than ever set on the shared Npc entity.
            playerUpdatePacketAddNpc.NotificationImageSetId = GetNotificationImageId(npc);

            SendTunneled(playerUpdatePacketAddNpc);
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
            // Quest badges ("!" offer / "?" turn-in) are per-player, so they override the NPC's static
            // Notification (e.g. a vendor badge) rather than being set on the shared entity.
            var questImageId = GetNotificationImageId(npc);
            if (questImageId != 0)
            {
                playerUpdatePacketAddNotifications.Notifications.Add(new NotificationInfo
                {
                    Guid = npc.Guid,
                    Combat = false,
                    ImageId = questImageId,
                    NameId = npc.NameId,
                    SubTextId = npc.SubTextNameId,
                });
            }
            else if (npc.Notification is not null)
            {
                playerUpdatePacketAddNotifications.Notifications.Add(npc.Notification);
            }
        }

        if (playerUpdatePacketAddNotifications.Notifications.Count > 0)
            SendTunneled(playerUpdatePacketAddNotifications);

        foreach (var npc in npcs)
            VisibleNpcs.TryAdd(npc.Guid, npc);
    }

    // Quest badges are per-player (unlike vendor badges, which are static on the Npc entity), since they
    // depend on this player's own quest progress. "!" if the NPC gives a quest the player can currently
    // take, "?" if the player has an active quest that turns in here, else the NPC's own static badge.
    public int GetNotificationImageId(Npc npc)
    {
        var quests = _resourceManager.Quests;

        if (quests.ByGiver.TryGetValue(npc.Guid, out var giverQuestIds))
        {
            foreach (var questId in giverQuestIds)
            {
                if (quests.TryGet(questId, out var quest) && quest.IsOfferableFor(Quests))
                    return quest.NotificationAvailable;
            }
        }

        if (quests.ByTarget.TryGetValue(npc.Guid, out var targetQuestIds))
        {
            foreach (var questId in targetQuestIds)
            {
                if (Quests.TryGetValue(questId, out var completed) && !completed && quests.TryGet(questId, out var quest))
                    return quest.NotificationActive;
            }
        }

        return npc.Notification?.ImageId ?? 0;
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

            // playerUpdatePacketAddPc.TemporaryAppearance = 277;

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
