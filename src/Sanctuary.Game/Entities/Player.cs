using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

using Sanctuary.Core.IO;
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

    public ClientPcProfile ActiveProfile => Profiles.FirstOrDefault(x => x.Id == ActiveProfileId)
        ?? Profiles.FirstOrDefault()
        ?? new ClientPcProfile { Id = ActiveProfileId };

    public Mount? Mount { get; set; }

    public List<FriendData> Friends { get; set; } = [];
    public List<IgnoreData> Ignores { get; set; } = [];

    public ConcurrentDictionary<ChatChannel, bool> ChatChannelStatus { get; set; } = [];

    public ConcurrentDictionary<int, int> ItemActionBarSlots { get; } = [];

    public int StationCash { get; set; }
    public bool IsAdmin { get; set; }
    public List<CoinStoreTransactionRecord> CoinStoreTransactions { get; set; } = [];

    public GuildData? GuildData { get; set; }

    public Vector4 StartingZonePosition { get; set; }
    public Quaternion StartingZoneRotation { get; set; }
    public Vector4 LastGroundedPosition { get; private set; }
    public bool HasLastGroundedPosition { get; private set; }

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

    #endregion

    #region Update

    public void UpdateEveryTick()
    {
    }

    public void UpdateEverySecond()
    {
    }

    public void UpdatePosition(Vector4 position, Quaternion rotation)
    {
        UpdatePosition(position, rotation, updateGroundedPosition: true);
    }

    public void UpdatePosition(Vector4 position, Quaternion rotation, bool updateGroundedPosition)
    {
        Position = position;
        Rotation = rotation;

        if (updateGroundedPosition)
        {
            LastGroundedPosition = position;
            HasLastGroundedPosition = true;
        }

        if (Visible)
        {
            UpdateZoneTile();

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

        if (Zone is StartingZone && Zone is not CombatInstanceZone)
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
            var playerUpdatePacketAddNpc = npc.GetAddNpcPacket();

            SendTunneled(playerUpdatePacketAddNpc);

            VisibleNpcs.TryAdd(npc.Guid, npc);
        }

        var playerUpdatePacketNpcRelevance = new PlayerUpdatePacketNpcRelevance();

        foreach (var npc in npcs)
        {
            if (npc.CursorId == 0)
                continue;

            playerUpdatePacketNpcRelevance.Entries.Add(new PlayerUpdatePacketNpcRelevance.Entry
            {
                Guid = npc.Guid,
                Unknown = true,
                CursorId = npc.CursorId,
                Unknown2 = false
            });
        }

        if (playerUpdatePacketNpcRelevance.Entries.Count > 0)
            SendTunneled(playerUpdatePacketNpcRelevance);

        /* var playerUpdatePacketAddNotifications = new PlayerUpdatePacketAddNotifications();

        foreach (var npc in npcs)
        {
            if (npc.Notification is null)
                continue;

            playerUpdatePacketAddNotifications.Notifications.Add(npc.Notification);

            SendTunneled(playerUpdatePacketAddNotifications);
        }

        foreach (var npc in npcs)
            VisibleNpcs.TryAdd(npc.Guid, npc); */
    }

    public void OnAddVisiblePlayers(params IEnumerable<Player> players)
    {
        foreach (var player in players)
        {
            var playerUpdatePacketAddPc = player.GetAddPcPacket();

            SendTunneled(playerUpdatePacketAddPc);
        }

        foreach (var player in players)
            VisiblePlayers.TryAdd(player.Guid, player);
    }

    public void OnRemoveVisibleNpcs(params IEnumerable<Npc> npcs)
    {
        foreach (var npc in npcs)
        {
            if (npc is Mount mount)
            {
                var playerUpdatePacketRemovePlayerGracefully = new PlayerUpdatePacketRemovePlayerGracefully();

                playerUpdatePacketRemovePlayerGracefully.Guid = npc.Guid;

                playerUpdatePacketRemovePlayerGracefully.Animate = false;
                playerUpdatePacketRemovePlayerGracefully.Delay = 0;
                playerUpdatePacketRemovePlayerGracefully.EffectDelay = 0;
                playerUpdatePacketRemovePlayerGracefully.CompositeEffectId = 46;
                playerUpdatePacketRemovePlayerGracefully.Duration = 1000;

                SendTunneled(playerUpdatePacketRemovePlayerGracefully);
            }
            else
            {
                var playerUpdatePacketRemovePlayer = new PlayerUpdatePacketRemovePlayer();

                playerUpdatePacketRemovePlayer.Guid = npc.Guid;

                SendTunneled(playerUpdatePacketRemovePlayer);
            }
        }

        foreach (var npc in npcs)
            VisibleNpcs.TryRemove(npc.Guid, out _);
    }

    public void OnRemoveVisiblePlayers(params IEnumerable<Player> players)
    {
        foreach (var player in players)
        {
            var playerUpdatePacketRemovePlayer = new PlayerUpdatePacketRemovePlayer();

            playerUpdatePacketRemovePlayer.Guid = player.Guid;

            SendTunneled(playerUpdatePacketRemovePlayer);
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

    public int GetEquippedWeaponDefinitionId()
    {
        const int WeaponSlot = 7;

        if (!ActiveProfile.Items.TryGetValue(WeaponSlot, out var profileItem))
            return 0;

        var clientItem = Items.FirstOrDefault(x => x.Id == profileItem.Id);

        return clientItem?.Definition ?? 0;
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

            Debug.WriteLine($"AddPc: {Name} {Guid} | {Mount.Guid} {Mount.Seat} {Mount.QueuePosition}");
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

        if (Mount is not null)
        {
            Mount.ZoneTile.Entities.Remove(Mount.Guid, out _);

            Zone.TryRemoveNpc(Mount.Guid);
            Mount = null;
        }

        ZoneTile.Entities.Remove(Guid, out _);

        Zone.TryRemovePlayer(Guid);
    }

    // Spawns an NPC at the player's position/rotation.
    // Chat: !spawnnpc <nameId> <modelId> [scale] [textureAlias...]
    public Npc? SpawnNpc(
        int nameId,
        int modelId,
        float scale = 1.0f,
        string? textureAlias = null,
        Vector4? position = null,
        Quaternion? rotation = null,
        int compositeEffectId = 0,
        int animationId = 1)
    {
        if (Zone is null)
            return null;

        if (!Zone.TryCreateNpc(out var npc))
            return null;

        npc.Visible = true;
        npc.IsInteractable = true;

        npc.NameId = nameId;
        npc.ModelId = modelId;

        npc.TextureAlias = textureAlias ?? string.Empty;

        npc.Scale = (scale <= 0f) ? 1.0f : scale;
        npc.CompositeEffectId = compositeEffectId;
        npc.Animation = animationId;

        npc.IsCommandSpawned = true;
        npc.SpawnedByGuid = this.Guid;
        npc.CreatedAtUtc = DateTime.UtcNow;

        npc.UpdatePosition(position ?? Position, rotation ?? Rotation);

        if (npc.ZoneTile == ZoneTile.Empty)
        {
            Zone.TryRemoveNpc(npc.Guid);
            return null;
        }

        return npc;
    }
    public int ImportNpcsFromJson(string path, int count = int.MaxValue, int offset = 0)
    {
        if (Zone is null || !System.IO.File.Exists(path))
            return 0;

        var json = System.IO.File.ReadAllText(path);
        var list = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);

        if (list.ValueKind != System.Text.Json.JsonValueKind.Array)
            return 0;

        int imported = 0;
        int index = 0;

        foreach (var npcData in list.EnumerateArray())
        {
            if (index++ < offset)
                continue;

            if (imported >= count)
                break;

            if (!Zone.TryCreateNpc(out var npc))
                continue;

            npc.Visible = true;
            npc.IsInteractable = true;

            if (npcData.TryGetProperty("NameId", out var nameId))
                npc.NameId = nameId.GetInt32();

            if (npcData.TryGetProperty("ModelId", out var modelId))
                npc.ModelId = modelId.GetInt32();
            else if (npcData.TryGetProperty("Model Id", out var modelId2))
                npc.ModelId = modelId2.GetInt32();

            if (npcData.TryGetProperty("Name", out var name) && name.ValueKind == System.Text.Json.JsonValueKind.String)
                npc.Name = name.GetString();

            if (npcData.TryGetProperty("TextureAlias", out var tex) && tex.ValueKind == System.Text.Json.JsonValueKind.String)
                npc.TextureAlias = tex.GetString();
            else if (npcData.TryGetProperty("Texture Alias", out var tex2) && tex2.ValueKind == System.Text.Json.JsonValueKind.String)
                npc.TextureAlias = tex2.GetString();

            npc.Scale = 1.0f;
            npc.IsCommandSpawned = true;
            npc.SpawnedByGuid = this.Guid;
            npc.CreatedAtUtc = System.DateTime.UtcNow;

            float px = npcData.TryGetProperty("PositionX", out var p1) ? p1.GetSingle() : npcData.GetProperty("Position X").GetSingle();
            float py = npcData.TryGetProperty("PositionY", out var p2) ? p2.GetSingle() : npcData.GetProperty("Position Y").GetSingle();
            float pz = npcData.TryGetProperty("PositionZ", out var p3) ? p3.GetSingle() : npcData.GetProperty("Position Z").GetSingle();

            float rx = npcData.TryGetProperty("RotationX", out var r1) ? r1.GetSingle() : npcData.GetProperty("Rotation X").GetSingle();
            float ry = npcData.TryGetProperty("RotationY", out var r2) ? r2.GetSingle() : (npcData.TryGetProperty("Rotation Y", out var r2b) ? r2b.GetSingle() : 0f);

            float rz = 0f;
            if (npcData.TryGetProperty("RotationZ", out var r3))
                rz = r3.ValueKind == System.Text.Json.JsonValueKind.String ? float.Parse(r3.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture) : r3.GetSingle();
            else if (npcData.TryGetProperty("Rotation Z", out var r3b))
                rz = r3b.ValueKind == System.Text.Json.JsonValueKind.String ? float.Parse(r3b.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture) : r3b.GetSingle();

            npc.UpdatePosition(
                new System.Numerics.Vector4(px, py, pz, 1f),
                new System.Numerics.Quaternion(rx, ry, rz, 1f)
            );

            if (npc.ZoneTile == ZoneTile.Empty)
            {
                Zone.TryRemoveNpc(npc.Guid);
                continue;
            }

            imported++;
        }

        return imported;
    }

    public int ImportNpcsFromJsonFiles(string directory, int count = int.MaxValue, int offset = 0)
    {
        if (!System.IO.Directory.Exists(directory))
            return 0;

        int imported = 0;

        foreach (var file in System.IO.Directory.GetFiles(directory, "*.json"))
        {
            var importedFromFile = ImportNpcsFromJson(file, count, offset);
            imported += importedFromFile;
        }

        return imported;
    }

    public int BackupCommandSpawnedNpcsToJson(string path)
    {
        if (Zone is null)
            return 0;

        var npcs = Zone.Npcs
            .Where(n => n.IsCommandSpawned)
            .Select(n => new
            {
                n.NameId,
                n.Name,
                n.ModelId,
                n.TextureAlias,
                PositionX = n.Position.X,
                PositionY = n.Position.Y,
                PositionZ = n.Position.Z,
                RotationX = n.Rotation.X,
                RotationY = n.Rotation.Y,
                RotationZ = n.Rotation.Z,
                n.Scale
            })
            .ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(npcs, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        System.IO.File.WriteAllText(path, json);
        return npcs.Count;
    }

    public void Disconnect(int flushTimeout = 0)
    {
        _connection.Disconnect(flushTimeout);
    }


}
