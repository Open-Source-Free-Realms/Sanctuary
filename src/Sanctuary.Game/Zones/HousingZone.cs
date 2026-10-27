using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Interactions;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Zones;

public sealed class HousingZone : BaseZone
{
    public const int MaxFixtureCount = 500;
    public const int MaxLandmarkCount = 10;

    /// <summary>
    /// <see cref="InstancePermission.Level"/> that allows editing the house.
    /// </summary>
    private const int EditPermissionLevel = 3;

    private const int MaxHouseNameLength = 32;

    /// <summary>
    /// The client's default head size in housing zones.
    /// </summary>
    private const int DefaultHeadSize = 10;

    /// <summary>
    /// PFX_pixie_dust_med_interact_loop, the sparkle on clickable world objects.
    /// </summary>
    private const int ExitEffectId = 1107;

    /// <summary>
    /// Same cursor as warpstones.
    /// </summary>
    private const byte ExitCursorId = 17;

    /// <summary>
    /// sg_exit_door_01.adr
    /// </summary>
    private const int ExitDoorModelId = 846;

    private readonly IHousingManager _housingManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly object _houseLock = new();

    private readonly ConcurrentDictionary<ulong, byte> _editors = new();
    private readonly Dictionary<ulong, DbHouseFixture> _fixtures = new();

    /// <summary>
    /// The client renders fixtures through an NPC actor, keyed by <see cref="FixtureInstance.NpcGuid"/>.
    /// </summary>
    private readonly Dictionary<ulong, Npc> _fixtureNpcs = new();

    /// <summary>
    /// The fixture each player last placed or moved, the target of <see cref="RotateLastFixture"/>.
    /// </summary>
    private readonly ConcurrentDictionary<ulong, ulong> _lastFixtures = new();

    private DbHouse _house = null!;
    private HouseDefinition _houseDefinition = null!;
    private string? _ownerName;

    public ulong HouseGuid => OwnerId ?? 0;
    public ulong OwnerGuid => GuidHelper.GetPlayerGuid(_house.CharacterId);

    public HousingZone(HousingZoneDefinition zoneDefinition, IServiceProvider serviceProvider)
        : base(zoneDefinition, serviceProvider)
    {
        _housingManager = serviceProvider.GetRequiredService<IHousingManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public override void OnStart()
    {
        lock (_houseLock)
        {
            if (_house is null)
            {
                LoadHouse();
                SpawnExit();

                foreach (var (fixtureGuid, fixture) in _fixtures)
                    SpawnFixtureNpc(fixtureGuid, fixture);
            }
        }

        base.OnStart();
    }

    /// <summary>
    /// Makes the lot gate / front door clickable to leave, with the usual interactable sparkle.
    /// Lots without a gate in their geometry get a standalone exit door at the spawn point.
    /// </summary>
    private void SpawnExit()
    {
        if (!TryCreateNpc(out var exit))
            return;

        exit.NameId = LeaveHouseInteraction.Data.ButtonText;
        exit.HideNamePlate = true;
        exit.CompositeEffectId = ExitEffectId;
        exit.CursorId = ExitCursorId;
        exit.AutoSelectSingleInteraction = true;
        exit.Interactions.Add(LeaveHouseInteraction.Data);
        exit.Visible = true;

        Vector4 position;

        if (_houseDefinition.ExitObjectId != 0)
        {
            exit.TerrainObjectId = _houseDefinition.ExitObjectId;
            position = _houseDefinition.ExitPosition;
        }
        else
        {
            exit.ModelId = ExitDoorModelId;
            position = _houseDefinition.SpawnPosition;
        }

        exit.UpdatePosition(position with { W = 1.0f }, Quaternion.Identity);
    }

    private void LoadHouse()
    {
        if (OwnerId is null || !GuidHelper.IsHouseGuid(OwnerId.Value))
            throw new InvalidOperationException($"Housing zone '{Name}' has no house guid.");

        using var dbContext = _dbContextFactory.CreateDbContext();

        var house = dbContext.Houses
            .AsNoTracking()
            .Include(x => x.Character)
            .Include(x => x.Fixtures)
            .SingleOrDefault(x => x.Id == GuidHelper.GetHouseId(OwnerId.Value));

        if (house is null)
            throw new InvalidOperationException($"House {OwnerId} does not exist.");

        if (!_resourceManager.Houses.TryGetValue(house.Definition, out var houseDefinition))
            throw new InvalidOperationException($"House definition {house.Definition} does not exist.");

        foreach (var fixture in house.Fixtures)
            _fixtures.Add(GuidHelper.GetFixtureGuid(fixture.Id), fixture);

        _ownerName = house.Character.FullName ?? house.Character.FirstName;
        _houseDefinition = houseDefinition;
        _house = house;
    }

    public override void OnClientIsReady(Player player)
    {
        // Everyone enters a house outside of edit mode.
        _editors.TryRemove(player.Guid, out _);

        base.OnClientIsReady(player);

        SendShopData(player);

        SendHouseData(player);

        _housingManager.SendPlayerHouses(player, player.Guid, null);
    }

    public bool IsOwner(Player player) => player.Guid == OwnerGuid;

    public bool IsEditing(Player player) => _editors.ContainsKey(player.Guid);

    #region House Data

    public void SendHouseData(Player player)
    {
        lock (_houseLock)
        {
            // Order matters: zone data turns the housing UI on (zoning turned it off), and the
            // fixture list and house info handlers use the instance without checking it exists.
            var instanceInfo = _housingManager.GetInstanceInfo(_house, _ownerName);

            instanceInfo.FixtureCount = _fixtures.Count;
            instanceInfo.FurnitureScore = _fixtures.Count;

            player.SendTunneled(new HousingPacketZoneData
            {
                IsInInstance = true,
                HeadSize = DefaultHeadSize,
                InstanceInfo = instanceInfo
            });

            player.SendTunneled(new HousingPacketInstanceData
            {
                InstanceData = GetInstanceData()
            });

            player.SendTunneled(GetFixtureItemList(player));

            player.SendTunneled(GetUpdateHouseInfo(player));
        }
    }

    private PlayerHousingInstanceData GetInstanceData()
    {
        var instanceData = new PlayerHousingInstanceData
        {
            HouseGuid = HouseGuid,
            OwnerGuid = OwnerGuid,
            OwnerName = _ownerName,
            NameId = _house.NameId != 0 ? _house.NameId : _houseDefinition.NameId,
            Name = _house.Name,
            IsLocked = _house.IsLocked,
            IsFloraAllowed = _house.IsFloraAllowed,
            MaxFixtureCount = MaxFixtureCount,
            MaxLandmarkCount = MaxLandmarkCount,
            CurFixtureCount = _fixtures.Count,
            IconId = _houseDefinition.Icon,
            FurnitureScore = _fixtures.Count,
            // The client tests fixture bounds against build areas on all four components,
            // so W must also enclose the fixture's (0 or 1).
            BuildAreas = GetBuildAreas()
        };

        foreach (var fixture in _fixtures.Values)
            instanceData.Fixtures.Add((uint)fixture.Id, GetFixtureInstance(fixture));

        instanceData.Permissions.Add(0, new InstancePermission
        {
            Guid = OwnerGuid,
            Level = EditPermissionLevel
        });

        return instanceData;
    }

    private HousingPacketUpdateHouseInfo GetUpdateHouseInfo(Player player)
    {
        return new HousingPacketUpdateHouseInfo
        {
            InEditMode = IsEditing(player),
            IsLocked = _house.IsLocked,
            IsFloraAllowed = _house.IsFloraAllowed,
            PetAutospawn = _house.PetAutospawn,
            CurFixtureCount = _fixtures.Count,
            FurnitureScore = _fixtures.Count
        };
    }

    private HousingPacketFixtureItemList GetFixtureItemList(Player player)
    {
        var housingPacketFixtureItemList = new HousingPacketFixtureItemList();

        var definitionIds = new HashSet<int>();

        foreach (var fixture in _fixtures.Values)
            definitionIds.Add(fixture.Definition);

        if (IsOwner(player))
        {
            foreach (var item in player.Items)
            {
                if (item.Count <= 0 || !_housingManager.TryGetFixtureDefinition(item.Definition, out _))
                    continue;

                housingPacketFixtureItemList.Infos.Add(new FixtureInstanceInfo
                {
                    FixtureGuid = (ulong)item.Id,
                    ItemDefinitionId = item.Definition
                });

                definitionIds.Add(item.Definition);
            }
        }

        foreach (var definitionId in definitionIds)
        {
            if (_housingManager.TryGetFixtureDefinition(definitionId, out var fixtureDefinition))
                housingPacketFixtureItemList.Definitions.Add(fixtureDefinition);
        }

        return housingPacketFixtureItemList;
    }

    private FixtureInstance GetFixtureInstance(DbHouseFixture fixture)
    {
        var fixtureInstance = new FixtureInstance
        {
            Guid = GuidHelper.GetFixtureGuid(fixture.Id),
            HouseGuid = HouseGuid,
            FixtureDefinitionId = fixture.Definition,
            Position = new Vector4(fixture.PositionX, fixture.PositionY, fixture.PositionZ, 1.0f),
            Rotation = new Quaternion(fixture.RotationX, fixture.RotationY, fixture.RotationZ, fixture.RotationW),
            Scale = fixture.Scale,
            TintId = fixture.Tint,
            NpcGuid = _fixtureNpcs.TryGetValue(GuidHelper.GetFixtureGuid(fixture.Id), out var npc) ? npc.Guid : 0
        };

        if (_resourceManager.ClientItemDefinitions.TryGetValue(fixture.Definition, out var modelItem))
        {
            // The client keys its fixture assets by this name and the definition's Category;
            // with either empty, editing (move, turn, ...) silently does nothing.
            fixtureInstance.Unknown12 = modelItem.ModelName;

            // Experimental: let the client build the actor itself, as it does for the placement ghost.
            if (UsesClientFixtureActors)
                fixtureInstance.NpcGuid = fixtureInstance.Guid;
        }

        if (_resourceManager.ClientItemDefinitions.TryGetValue(fixture.Definition, out var itemDefinition)
            && !string.IsNullOrEmpty(itemDefinition.TextureAlias))
        {
            fixtureInstance.Customization.Type = 1;
            fixtureInstance.Customization.TextureAlias = itemDefinition.TextureAlias;
            fixtureInstance.Customization.TintAlias = itemDefinition.TintAlias;
            fixtureInstance.Customization.TintId = fixture.Tint;
        }

        return fixtureInstance;
    }

    private void SpawnFixtureNpc(ulong fixtureGuid, DbHouseFixture fixture)
    {
        if (UsesClientFixtureActors)
            return;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(fixture.Definition, out var itemDefinition)
            || !_housingManager.TryGetFixtureDefinition(fixture.Definition, out var fixtureDefinition))
        {
            Logger.LogWarning("Fixture {id} uses item {definition}, which has no model.", fixture.Id, fixture.Definition);
            return;
        }

        if (!TryCreateNpc(out var npc))
            return;

        npc.NameId = itemDefinition.NameId;
        npc.ModelId = fixtureDefinition.ModelId;
        npc.TextureAlias = itemDefinition.TextureAlias;
        npc.TintAlias = itemDefinition.TintAlias;
        npc.TintId = fixture.Tint;
        npc.Scale = fixture.Scale;
        npc.HideNamePlate = true;
        npc.IsInteractable = false;
        npc.IsHouseFixture = true;
        npc.Visible = true;

        var position = new Vector4(fixture.PositionX, fixture.PositionY, fixture.PositionZ, 1.0f);
        var rotation = new Quaternion(fixture.RotationX, fixture.RotationY, fixture.RotationZ, fixture.RotationW);

        npc.SpawnPosition = position;
        npc.SpawnRotation = rotation;

        npc.UpdatePosition(position, rotation);

        _fixtureNpcs[fixtureGuid] = npc;
    }

    private void DespawnFixtureNpc(ulong fixtureGuid)
    {
        if (_fixtureNpcs.Remove(fixtureGuid, out var npc))
            npc.Dispose();
    }

    private void BroadcastHouseInfo()
    {
        foreach (var player in Players)
            player.SendTunneled(GetUpdateHouseInfo(player));
    }

    private void BroadcastInstanceData()
    {
        var housingPacketInstanceData = new HousingPacketInstanceData
        {
            InstanceData = GetInstanceData()
        };

        foreach (var player in Players)
            player.SendTunneled(housingPacketInstanceData);
    }

    private void Broadcast(ISerializablePacket packet)
    {
        foreach (var player in Players)
            player.SendTunneled(packet);
    }

    #endregion

    #region Edit Mode

    public void SetEditMode(Player player, bool inEditMode)
    {
        lock (_houseLock)
        {
            if (inEditMode && IsOwner(player))
                _editors.TryAdd(player.Guid, 0);
            else
                _editors.TryRemove(player.Guid, out _);

            player.SendTunneled(GetUpdateHouseInfo(player));
        }
    }

    private bool CanEdit(Player player)
    {
        if (!IsOwner(player))
        {
            DenyPlacement(player, "You do not have permission to edit this house.");
            return false;
        }

        if (!IsEditing(player))
        {
            DenyPlacement(player, "You must be in housing edit mode to do this.");
            return false;
        }

        return true;
    }

    private void DenyPlacement(Player player, string message)
    {
        Logger.LogInformation("Denied fixture placement for {player}: {message}", player.Guid, message);

        player.SendTunneled(new HousingPacketFixturePlacementDenied
        {
            Message = message
        });
    }

    #endregion

    #region Fixtures

    public void RequestFixturePlacement(Player player, ClientHousingPacketPlaceFixtureRequest packet)
    {
        Logger.LogInformation("{player} requested placement of item {itemGuid} (definition {definition}, tint {tint}, immediate {immediate}).",
            player.Guid, packet.ItemGuid, packet.ItemDefinitionId, packet.TintId, packet.PlaceImmediately);

        lock (_houseLock)
        {
            if (!CanEdit(player))
                return;

            if (!TryGetFixtureItem(player, packet.ItemGuid, packet.ItemDefinitionId, packet.TintId, out var item, out var fixtureDefinition))
            {
                DenyPlacement(player, "You don't have any more of that item.");
                return;
            }

            if (_fixtures.Count >= MaxFixtureCount)
            {
                DenyPlacement(player, "This house can't hold any more furniture.");
                return;
            }

            _resourceManager.ClientItemDefinitions.TryGetValue(item.Definition, out var itemDefinition);

            player.SendTunneled(new HousingPacketFixtureAsset
            {
                ModelId = fixtureDefinition.ModelId,
                ItemGuid = item.Id,
                Definition = fixtureDefinition,
                TextureAlias = itemDefinition?.TextureAlias,
                TintAlias = itemDefinition?.TintAlias,
                TintId = item.Tint,
                PlaceImmediately = packet.PlaceImmediately
            });
        }
    }

    public void PlaceFixture(Player player, ClientHousingPacketPlaceFixture packet)
    {
        Logger.LogInformation("{player} placed item {itemGuid} at {position}.", player.Guid, packet.ItemGuid, packet.Position);

        lock (_houseLock)
        {
            if (!CanEdit(player))
                return;

            if (!TryGetFixtureItem(player, packet.ItemGuid, -1, -1, out var item, out _))
            {
                DenyPlacement(player, "You don't have any more of that item.");
                return;
            }

            if (_fixtures.Count >= MaxFixtureCount)
            {
                DenyPlacement(player, "This house can't hold any more furniture.");
                return;
            }

            if (!IsInBuildArea(packet.Position))
            {
                DenyPlacement(player, "That fixture can not be placed there.");
                return;
            }

            var characterId = GuidHelper.GetPlayerId(player.Guid);

            using var dbContext = _dbContextFactory.CreateDbContext();

            var dbItem = dbContext.Items.SingleOrDefault(x => x.Id == item.Id && x.CharacterId == characterId);

            if (dbItem is null || dbItem.Count <= 0)
            {
                DenyPlacement(player, "You don't have any more of that item.");
                return;
            }

            var fixture = new DbHouseFixture
            {
                HouseId = _house.Id,
                Definition = item.Definition,
                Tint = item.Tint,
                PositionX = packet.Position.X,
                PositionY = packet.Position.Y,
                PositionZ = packet.Position.Z,
                RotationX = packet.Rotation.X,
                RotationY = packet.Rotation.Y,
                RotationZ = packet.Rotation.Z,
                RotationW = packet.Rotation.W,
                Scale = packet.Scale > 0.0f ? packet.Scale : 1.0f
            };

            dbContext.HouseFixtures.Add(fixture);

            var deleteItem = dbItem.Count <= 1;

            if (deleteItem)
                dbContext.Items.Remove(dbItem);
            else
                dbItem.Count--;

            if (dbContext.SaveChanges() <= 0)
            {
                DenyPlacement(player, "Fixture placement denied.");
                return;
            }

            fixture.House = null!;
            var fixtureGuid = GuidHelper.GetFixtureGuid(fixture.Id);

            _fixtures.Add(fixtureGuid, fixture);
            _lastFixtures[player.Guid] = fixtureGuid;
            SpawnFixtureNpc(fixtureGuid, fixture);

            if (deleteItem)
            {
                player.Items.Remove(item);
                player.SendTunneled(new ClientUpdatePacketItemDelete { ItemGuid = item.Id });
            }
            else
            {
                item.Count = dbItem.Count;
                player.SendTunneled(new ClientUpdatePacketItemUpdate { ItemGuid = item.Id, Count = item.Count });
            }

            Broadcast(GetFixtureUpdate(fixture));

            player.SendTunneled(GetFixtureItemList(player));

            BroadcastHouseInfo();
        }
    }

    public void SaveFixture(Player player, ClientHousingPacketSaveFixture packet)
    {
        lock (_houseLock)
        {
            if (!CanEdit(player))
                return;

            if (!_fixtures.TryGetValue(packet.FixtureGuid, out var fixture))
                return;

            _lastFixtures[player.Guid] = packet.FixtureGuid;

            if (!IsInBuildArea(packet.Position))
            {
                DenyPlacement(player, "That fixture can not be placed there.");

                // Snap the client back to the last saved position.
                player.SendTunneled(GetUpdateFixturePosition(fixture));
                return;
            }

            using var dbContext = _dbContextFactory.CreateDbContext();

            var dbFixture = dbContext.HouseFixtures.SingleOrDefault(x => x.Id == fixture.Id);

            if (dbFixture is null)
                return;

            var tint = packet.TintId > 0 ? packet.TintId : fixture.Tint;
            var scale = packet.Scale > 0.0f ? packet.Scale : fixture.Scale;
            var appearanceChanged = tint != fixture.Tint || scale != fixture.Scale;

            dbFixture.PositionX = packet.Position.X;
            dbFixture.PositionY = packet.Position.Y;
            dbFixture.PositionZ = packet.Position.Z;
            dbFixture.RotationX = packet.Rotation.X;
            dbFixture.RotationY = packet.Rotation.Y;
            dbFixture.RotationZ = packet.Rotation.Z;
            dbFixture.RotationW = packet.Rotation.W;
            dbFixture.Tint = tint;
            dbFixture.Scale = scale;

            dbContext.SaveChanges();

            fixture.PositionX = dbFixture.PositionX;
            fixture.PositionY = dbFixture.PositionY;
            fixture.PositionZ = dbFixture.PositionZ;
            fixture.RotationX = dbFixture.RotationX;
            fixture.RotationY = dbFixture.RotationY;
            fixture.RotationZ = dbFixture.RotationZ;
            fixture.RotationW = dbFixture.RotationW;
            fixture.Tint = dbFixture.Tint;
            fixture.Scale = dbFixture.Scale;

            if (appearanceChanged)
            {
                Broadcast(new HousingPacketFixtureRemove { FixtureGuid = packet.FixtureGuid });

                DespawnFixtureNpc(packet.FixtureGuid);
                SpawnFixtureNpc(packet.FixtureGuid, fixture);

                Broadcast(GetFixtureUpdate(fixture));
            }
            else
            {
                if (_fixtureNpcs.TryGetValue(packet.FixtureGuid, out var npc))
                    npc.UpdatePosition(packet.Position with { W = 1.0f }, packet.Rotation);

                Broadcast(GetUpdateFixturePosition(fixture));
            }
        }
    }

    /// <summary>
    /// Rotates the player's last placed or moved fixture about its own yaw (Y), pitch (X) or roll (Z) axis.
    /// The client's own turn control only does fixed yaw steps.
    /// </summary>
    public bool RotateLastFixture(Player player, char axis, float degrees, out string message)
    {
        lock (_houseLock)
        {
            if (!IsOwner(player) || !IsEditing(player))
            {
                message = "Turn on edit mode in your own house first.";
                return false;
            }

            if (!_lastFixtures.TryGetValue(player.Guid, out var fixtureGuid) || !_fixtures.TryGetValue(fixtureGuid, out var fixture))
            {
                message = "Place or move a piece of furniture first.";
                return false;
            }

            var radians = degrees * MathF.PI / 180.0f;
            var delta = axis switch
            {
                'p' => Quaternion.CreateFromAxisAngle(Vector3.UnitX, radians),
                'r' => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, radians),
                _ => Quaternion.CreateFromAxisAngle(Vector3.UnitY, radians)
            };

            var rotation = new Quaternion(fixture.RotationX, fixture.RotationY, fixture.RotationZ, fixture.RotationW);

            SaveFixture(player, new ClientHousingPacketSaveFixture
            {
                FixtureGuid = fixtureGuid,
                Position = new Vector4(fixture.PositionX, fixture.PositionY, fixture.PositionZ, 1.0f),
                Rotation = Quaternion.Normalize(rotation * delta),
                TintId = fixture.Tint,
                Scale = fixture.Scale
            });

            message = $"Rotated {(axis == 'p' ? "pitch" : axis == 'r' ? "roll" : "yaw")} by {degrees:0.#} degrees.";
            return true;
        }
    }

    public void PickupFixture(Player player, ulong fixtureGuid)
    {
        lock (_houseLock)
        {
            if (!CanEdit(player))
                return;

            if (!_fixtures.TryGetValue(fixtureGuid, out var fixture))
                return;

            if (!TryReturnFixtures(player, [fixture]))
                return;

            player.SendTunneled(GetFixtureItemList(player));

            BroadcastHouseInfo();
        }
    }

    public void PickupAllFixtures(Player player)
    {
        lock (_houseLock)
        {
            if (!CanEdit(player))
                return;

            if (_fixtures.Count == 0)
                return;

            if (!TryReturnFixtures(player, _fixtures.Values.ToList()))
                return;

            player.SendTunneled(GetFixtureItemList(player));

            BroadcastHouseInfo();
        }
    }

    /// <summary>
    /// Removes the fixtures from the house and puts the items back in the player's inventory.
    /// </summary>
    private bool TryReturnFixtures(Player player, List<DbHouseFixture> fixtures)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters
            .Include(x => x.Items)
            .SingleOrDefault(x => x.Id == characterId);

        if (dbCharacter is null)
            return false;

        var fixtureIds = fixtures.Select(x => x.Id).ToList();

        var dbFixtures = dbContext.HouseFixtures
            .Where(x => x.HouseId == _house.Id && fixtureIds.Contains(x.Id))
            .ToList();

        if (dbFixtures.Count == 0)
            return false;

        var changedItems = new List<DbItem>();
        var addedItems = new List<DbItem>();

        foreach (var dbFixture in dbFixtures)
        {
            var dbItem = dbCharacter.Items.SingleOrDefault(x => x.Definition == dbFixture.Definition && x.Tint == dbFixture.Tint);

            if (dbItem is null)
            {
                dbItem = new DbItem
                {
                    Id = dbCharacter.Items.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1,
                    Definition = dbFixture.Definition,
                    Tint = dbFixture.Tint
                };

                dbCharacter.Items.Add(dbItem);
                addedItems.Add(dbItem);
            }
            else if (!changedItems.Contains(dbItem) && !addedItems.Contains(dbItem))
            {
                changedItems.Add(dbItem);
            }

            dbItem.Count++;

            dbContext.HouseFixtures.Remove(dbFixture);
        }

        if (dbContext.SaveChanges() <= 0)
            return false;

        foreach (var dbFixture in dbFixtures)
        {
            var fixtureGuid = GuidHelper.GetFixtureGuid(dbFixture.Id);

            _fixtures.Remove(fixtureGuid);

            Broadcast(new HousingPacketFixtureRemove { FixtureGuid = fixtureGuid });

            DespawnFixtureNpc(fixtureGuid);
        }

        foreach (var dbItem in addedItems)
        {
            if (!_resourceManager.ClientItemDefinitions.TryGetValue(dbItem.Definition, out var itemDefinition))
                continue;

            var clientItem = new ClientItem
            {
                Id = dbItem.Id,
                Definition = dbItem.Definition,
                Count = dbItem.Count,
                Tint = dbItem.Tint
            };

            player.Items.Add(clientItem);

            using var writer = new PacketWriter();

            clientItem.Serialize(writer);
            itemDefinition.Serialize(writer);

            player.SendTunneled(new ClientUpdatePacketItemAdd { Payload = writer.Buffer });
        }

        foreach (var dbItem in changedItems)
        {
            var clientItem = player.Items.SingleOrDefault(x => x.Id == dbItem.Id);

            if (clientItem is null)
                continue;

            clientItem.Count = dbItem.Count;

            player.SendTunneled(new ClientUpdatePacketItemUpdate { ItemGuid = clientItem.Id, Count = clientItem.Count });
        }

        return true;
    }

    private bool TryGetFixtureItem(Player player, int itemGuid, int itemDefinitionId, int tintId,
        out ClientItem item, out FixtureDefinition fixtureDefinition)
    {
        // The client sends the item guid when placing from the furniture list, and only the
        // definition + tint when placing another copy of the fixture it just placed.
        var clientItem = itemGuid > 0
            ? player.Items.SingleOrDefault(x => x.Id == itemGuid)
            : player.Items.FirstOrDefault(x => x.Definition == itemDefinitionId && (tintId < 0 || x.Tint == tintId));

        item = clientItem!;
        fixtureDefinition = null!;

        if (clientItem is null || clientItem.Count <= 0)
            return false;

        return _housingManager.TryGetFixtureDefinition(clientItem.Definition, out fixtureDefinition!);
    }

    /// <summary>
    /// Experimental: fixtures rendered by client-built actors instead of server NPCs.
    /// </summary>
    public bool UsesClientFixtureActors { get; private set; }

    public void SetClientFixtureActors(bool usesClientFixtureActors)
    {
        lock (_houseLock)
        {
            foreach (var fixtureGuid in _fixtures.Keys)
            {
                Broadcast(new HousingPacketFixtureRemove { FixtureGuid = fixtureGuid });
                DespawnFixtureNpc(fixtureGuid);
            }

            UsesClientFixtureActors = usesClientFixtureActors;

            foreach (var (fixtureGuid, fixture) in _fixtures)
            {
                SpawnFixtureNpc(fixtureGuid, fixture);
                Broadcast(GetFixtureUpdate(fixture));
            }
        }
    }

    /// <summary>
    /// GM override that lets fixtures be placed anywhere in the zone.
    /// </summary>
    public bool IsUnbound { get; private set; }

    public void SetUnbound(bool isUnbound)
    {
        lock (_houseLock)
        {
            IsUnbound = isUnbound;

            BroadcastInstanceData();
        }
    }

    private List<BoundingBox> GetBuildAreas()
    {
        // The client tests fixture bounds against build areas on all four components,
        // so W must also enclose the fixture's (0 or 1).
        if (IsUnbound)
        {
            return
            [
                new BoundingBox
                {
                    Min = new Vector4(-100_000.0f, -100_000.0f, -100_000.0f, -1.0f),
                    Max = new Vector4(100_000.0f, 100_000.0f, 100_000.0f, 1.0f)
                }
            ];
        }

        return _houseDefinition.BuildAreas
            .Select(x => new BoundingBox
            {
                Min = Vector4.Min(x.Min, x.Max) with { W = -1.0f },
                Max = Vector4.Max(x.Min, x.Max) with { W = 1.0f }
            })
            .ToList();
    }

    private bool IsInBuildArea(Vector4 position)
    {
        if (IsUnbound || _houseDefinition.BuildAreas.Count == 0)
            return true;

        // Some lots' data has Min and Max swapped on an axis; GetBuildAreas normalizes them.
        foreach (var buildArea in GetBuildAreas())
        {
            if (position.X >= buildArea.Min.X && position.X <= buildArea.Max.X &&
                position.Y >= buildArea.Min.Y && position.Y <= buildArea.Max.Y &&
                position.Z >= buildArea.Min.Z && position.Z <= buildArea.Max.Z)
                return true;
        }

        return false;
    }

    private HousingPacketFixtureUpdate GetFixtureUpdate(DbHouseFixture fixture)
    {
        var fixtureGuid = GuidHelper.GetFixtureGuid(fixture.Id);

        var housingPacketFixtureUpdate = new HousingPacketFixtureUpdate
        {
            Instance = GetFixtureInstance(fixture),
            Info = new FixtureInstanceInfo
            {
                FixtureGuid = fixtureGuid,
                ItemDefinitionId = fixture.Definition
            }
        };

        if (_housingManager.TryGetFixtureDefinition(fixture.Definition, out var fixtureDefinition))
            housingPacketFixtureUpdate.Definition = fixtureDefinition;

        return housingPacketFixtureUpdate;
    }

    private HousingPacketUpdateFixturePosition GetUpdateFixturePosition(DbHouseFixture fixture)
    {
        return new HousingPacketUpdateFixturePosition
        {
            FixtureGuid = GuidHelper.GetFixtureGuid(fixture.Id),
            Position = new Vector4(fixture.PositionX, fixture.PositionY, fixture.PositionZ, 1.0f),
            Rotation = new Quaternion(fixture.RotationX, fixture.RotationY, fixture.RotationZ, fixture.RotationW)
        };
    }

    #endregion

    #region Settings

    public void ToggleLocked(Player player) => SetLocked(player, !_house.IsLocked);

    public void SetLocked(Player player, bool isLocked)
    {
        lock (_houseLock)
        {
            if (!IsOwner(player))
                return;

            UpdateHouse(house => house.IsLocked = isLocked);

            BroadcastHouseInfo();
        }
    }

    public void ToggleFloraAllowed(Player player)
    {
        lock (_houseLock)
        {
            if (!IsOwner(player))
                return;

            UpdateHouse(house => house.IsFloraAllowed = !house.IsFloraAllowed);

            BroadcastHouseInfo();
        }
    }

    public void TogglePetAutospawn(Player player)
    {
        lock (_houseLock)
        {
            if (!IsOwner(player))
                return;

            UpdateHouse(house => house.PetAutospawn = !house.PetAutospawn);

            BroadcastHouseInfo();
        }
    }

    public void ChangeName(Player player, int nameId)
    {
        lock (_houseLock)
        {
            if (!IsOwner(player))
                return;

            UpdateHouse(house =>
            {
                house.NameId = nameId;
                house.Name = null;
            });

            BroadcastInstanceData();
        }
    }

    public void RequestName(Player player, string name)
    {
        lock (_houseLock)
        {
            var trimmedName = name.Trim();

            var accepted = IsOwner(player)
                && trimmedName.Length > 0
                && trimmedName.Length <= MaxHouseNameLength
                && !_resourceManager.NameFilter.Any(token => trimmedName.Contains(token, StringComparison.OrdinalIgnoreCase));

            if (accepted)
                UpdateHouse(house => house.Name = trimmedName);

            player.SendTunneled(new ClientHousingPacketRequestName
            {
                Name = name,
                Accepted = accepted
            });

            if (accepted)
                BroadcastInstanceData();
        }
    }

    private void UpdateHouse(Action<DbHouse> update)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouse = dbContext.Houses.SingleOrDefault(x => x.Id == _house.Id);

        if (dbHouse is null)
            return;

        update(dbHouse);

        if (dbContext.SaveChanges() <= 0)
            Logger.LogWarning("No changes saved for house {house}.", _house.Id);

        update(_house);

        // Keep the owner's house list (name, lock state) in sync.
        if (TryGetPlayer(OwnerGuid, out var owner))
            _housingManager.SendPlayerHouses(owner, owner.Guid, null);
    }

    #endregion
}
