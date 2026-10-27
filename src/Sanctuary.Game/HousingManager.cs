using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game;

public class HousingManager : IHousingManager
{
    /// <summary>
    /// Item category group containing every furniture category.
    /// </summary>
    private const int FurnitureCategoryGroupId = 50;

    /// <summary>
    /// Item type used by fixtures that don't live in a furniture category (e.g. farming props).
    /// </summary>
    private const int HousingItemType = 29;

    /// <summary>
    /// <see cref="FixtureDefinition.PlacementType"/> for regular furniture.
    /// </summary>
    private const int RegularFixturePlacementType = 1;

    private const string FixtureCategory = "Furniture";

    private readonly ILogger _logger;
    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly ConcurrentDictionary<int, FixtureDefinition> _fixtureDefinitions = new();

    private readonly Lazy<HashSet<int>> _furnitureCategories;
    private readonly Lazy<Dictionary<string, int>> _modelIds;

    public HousingManager(ILogger<HousingManager> logger, IZoneManager zoneManager, IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;

        _furnitureCategories = new(() =>
        {
            if (!_resourceManager.ItemCategoryGroups.TryGetValue(FurnitureCategoryGroupId, out var group))
                return [];

            return group.Select(x => x.CategoryId).ToHashSet();
        });

        _modelIds = new(() =>
        {
            var modelIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var model in _resourceManager.Models.Values)
                modelIds.TryAdd(model.ModelFileName, model.Id);

            return modelIds;
        });
    }

    public bool IsFurniture(ClientItemDefinition itemDefinition)
    {
        return itemDefinition.Type == HousingItemType || _furnitureCategories.Value.Contains(itemDefinition.CategoryId);
    }

    public bool TryGetFixtureDefinition(int itemDefinitionId, [MaybeNullWhen(false)] out FixtureDefinition fixtureDefinition)
    {
        if (_fixtureDefinitions.TryGetValue(itemDefinitionId, out fixtureDefinition))
            return true;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition) || !IsFurniture(itemDefinition))
        {
            fixtureDefinition = null;
            return false;
        }

        // Items without a model (roofing, wallpaper, ...) customize the house instead of being placed.
        if (string.IsNullOrEmpty(itemDefinition.ModelName) || !_modelIds.Value.TryGetValue(itemDefinition.ModelName, out var modelId))
        {
            fixtureDefinition = null;
            return false;
        }

        fixtureDefinition = _fixtureDefinitions.GetOrAdd(itemDefinitionId, _ => new FixtureDefinition
        {
            // Fixture definitions map 1:1 to the item they're placed from.
            Id = itemDefinitionId,
            ItemDefinitionId = itemDefinitionId,
            ModelId = modelId,
            CompositeEffectId = itemDefinition.CompositeEffectId,
            IsPlaceable = true,
            PlacementType = RegularFixturePlacementType,
            // Part of the key the client finds a fixture's assets by; must not be empty.
            Category = FixtureCategory
        });

        return true;
    }

    public List<DbHouse> GetHouses(ulong characterId)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Houses
            .AsNoTracking()
            .Include(house => house.Fixtures)
            .Where(house => house.CharacterId == characterId)
            .AsEnumerable()
            // SQLite can't order by unsigned 64-bit keys, so sort client-side.
            .OrderBy(house => house.Id)
            .ToList();
    }

    public void ProvisionHousesFromItems(Player player)
    {
        var ownedItemDefinitions = player.Items
            .Where(item => item.Count > 0)
            .Select(item => item.Definition)
            .ToHashSet();

        foreach (var houseDefinition in _resourceManager.Houses.Values)
        {
            if (!houseDefinition.ItemDefinitionIds.Any(ownedItemDefinitions.Contains))
                continue;

            TryGrantHouse(player, houseDefinition, out _);
        }
    }

    public bool TryGrantHouse(Player player, HouseDefinition houseDefinition, [MaybeNullWhen(false)] out DbHouse house)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        house = dbContext.Houses.SingleOrDefault(x => x.CharacterId == characterId && x.Definition == houseDefinition.Id);

        if (house is not null)
            return true;

        house = new DbHouse
        {
            Definition = houseDefinition.Id,
            NameId = houseDefinition.NameId,
            IsFloraAllowed = true,
            CharacterId = characterId
        };

        dbContext.Houses.Add(house);

        if (dbContext.SaveChanges() <= 0)
        {
            _logger.LogError("Failed to grant house {house} to {player}.", houseDefinition.Id, player.Guid);

            house = null;
            return false;
        }

        var housingPacketNotifyHouseAdded = new HousingPacketNotifyHouseAdded
        {
            NameId = houseDefinition.NameId,
            HouseGuid = GuidHelper.GetHouseGuid(house.Id),
            OwnerGuid = player.Guid
        };

        player.SendTunneled(housingPacketNotifyHouseAdded);

        SendHouseList(player, player.Guid, player.Name.FullName);

        return true;
    }

    public PlayerHousingInstanceInfo GetInstanceInfo(DbHouse house, string? ownerName)
    {
        _resourceManager.Houses.TryGetValue(house.Definition, out var houseDefinition);

        return new PlayerHousingInstanceInfo
        {
            OwnerGuid = GuidHelper.GetPlayerGuid(house.CharacterId),
            InstanceGuid = GuidHelper.GetHouseGuid(house.Id),
            NameId = house.NameId != 0 ? house.NameId : houseDefinition?.NameId ?? 0,
            OwnerName = ownerName,
            HouseName = house.Name,
            IconId = houseDefinition?.Icon ?? 0,
            FixtureCount = house.Fixtures.Count,
            FurnitureScore = house.Fixtures.Count,
            LastVisited = house.LastVisited ?? house.Created,
            IsLocked = house.IsLocked,
            IsFloraAllowed = house.IsFloraAllowed,
            WhenCreated = house.Created
        };
    }

    public void SendPlayerHouses(Player requester, ulong playerGuid, string? playerName)
    {
        var targetGuid = playerGuid;
        string? targetName = null;

        if (targetGuid == 0 && !string.IsNullOrWhiteSpace(playerName))
        {
            if (_zoneManager.TryGetPlayer(playerName, out var namedPlayer))
            {
                targetGuid = namedPlayer.Guid;
            }
            else
            {
                using var dbContext = _dbContextFactory.CreateDbContext();

                var characterId = dbContext.Characters
                    .Where(character => character.FullName == playerName)
                    .Select(character => (ulong?)character.Id)
                    .FirstOrDefault();

                if (characterId is not null)
                    targetGuid = GuidHelper.GetPlayerGuid(characterId.Value);
            }
        }

        if (targetGuid == 0)
            targetGuid = requester.Guid;

        if (targetGuid == requester.Guid)
        {
            ProvisionHousesFromItems(requester);

            targetName = requester.Name.FullName;
        }
        else if (_zoneManager.TryGetPlayer(targetGuid, out var targetPlayer))
        {
            targetName = targetPlayer.Name.FullName;
        }
        else
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            targetName = dbContext.Characters
                .Where(character => character.Id == GuidHelper.GetPlayerId(targetGuid))
                .Select(character => character.FullName)
                .FirstOrDefault();
        }

        SendHouseList(requester, targetGuid, targetName);
    }

    /// <summary>
    /// The client only learns about houses through this list (the "house added" packet is
    /// just a toast), so it's pushed whenever the player's own houses change.
    /// </summary>
    private void SendHouseList(Player requester, ulong targetGuid, string? targetName)
    {
        var housingPacketInstanceList = new HousingPacketInstanceList
        {
            PlayerGuid = targetGuid
        };

        foreach (var house in GetHouses(GuidHelper.GetPlayerId(targetGuid)))
            housingPacketInstanceList.Instances.Add(GetInstanceInfo(house, targetName));

        requester.SendTunneled(housingPacketInstanceList);
    }

    public bool TryEnterHouse(Player player, ulong houseGuid)
    {
        if (!GuidHelper.IsHouseGuid(houseGuid))
            return false;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var house = dbContext.Houses
            .Include(x => x.Character)
            .Include(x => x.Fixtures)
            .SingleOrDefault(x => x.Id == GuidHelper.GetHouseId(houseGuid));

        if (house is null)
        {
            ChatHelper.SendSystemMessage(player, "That house could not be found.");
            return false;
        }

        if (!_resourceManager.Houses.TryGetValue(house.Definition, out var houseDefinition)
            || !_resourceManager.Zones.ContainsKey(houseDefinition.ZoneId))
        {
            _logger.LogError("House {house} has no housing zone for definition {definition}.", house.Id, house.Definition);

            ChatHelper.SendSystemMessage(player, "That housing zone could not be found.");
            return false;
        }

        var isOwner = GuidHelper.GetPlayerGuid(house.CharacterId) == player.Guid;

        if (house.IsLocked && !isOwner)
        {
            ChatHelper.SendSystemMessage(player, "That house is locked.");
            return false;
        }

        if (player.Zone is HousingZone currentHouse && currentHouse.OwnerId == houseGuid)
            return true;

        house.LastVisited = DateTimeOffset.UtcNow;
        dbContext.SaveChanges();

        // The housing zone data itself is sent by the zone once the client has loaded it.
        var spawnRotation = new Quaternion(
            houseDefinition.SpawnRotation.X,
            houseDefinition.SpawnRotation.Y,
            houseDefinition.SpawnRotation.Z,
            houseDefinition.SpawnRotation.W);

        if (!player.TeleportToZone(houseDefinition.ZoneId, houseGuid, houseDefinition.SpawnPosition, spawnRotation))
        {
            _logger.LogError("Failed to move {player} into house {house}.", player.Guid, house.Id);

            ChatHelper.SendSystemMessage(player, "The housing zone failed to launch.");
            return false;
        }

        return true;
    }

    public bool TryLeaveHouse(Player player)
    {
        if (player.Zone is not HousingZone)
            return false;

        var startingZone = _zoneManager.StartingZone;

        var position = player.StartingZonePosition;
        var rotation = player.StartingZoneRotation;

        if (position == Vector4.Zero)
        {
            position = startingZone.SpawnPosition;
            rotation = startingZone.SpawnRotation;
        }

        return player.TeleportToZone(startingZone.DefinitionId, null, position, rotation);
    }

    public bool TryVisitPlayer(Player player, ulong playerGuid)
    {
        var isSelf = playerGuid == player.Guid;

        if (isSelf)
            ProvisionHousesFromItems(player);

        var house = GetHouses(GuidHelper.GetPlayerId(playerGuid))
            .FirstOrDefault(x => isSelf || !x.IsLocked);

        if (house is null)
        {
            ChatHelper.SendSystemMessage(player, "That player doesn't have a house you can visit.");
            return false;
        }

        return TryEnterHouse(player, GuidHelper.GetHouseGuid(house.Id));
    }

    /// <summary>
    /// Tops every placeable furniture item up to <paramref name="quantity"/> in the player's inventory.
    /// </summary>
    /// <returns>The number of item stacks that were added or changed.</returns>
    public int GrantAllFurniture(Player player, int quantity)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters
            .Include(x => x.Items)
            .SingleOrDefault(x => x.Id == characterId);

        if (dbCharacter is null)
            return 0;

        var lastItemId = dbCharacter.Items.Select(x => x.Id).DefaultIfEmpty(0).Max();

        var addedItems = new List<(DbItem Item, ClientItemDefinition Definition)>();
        var changedItems = new List<DbItem>();

        foreach (var itemDefinition in _resourceManager.ClientItemDefinitions.Values)
        {
            if (!TryGetFixtureDefinition(itemDefinition.Id, out _))
                continue;

            var dbItem = dbCharacter.Items.FirstOrDefault(x => x.Definition == itemDefinition.Id && x.Tint == 0);

            if (dbItem is null)
            {
                dbItem = new DbItem
                {
                    Id = ++lastItemId,
                    Definition = itemDefinition.Id,
                    Count = quantity
                };

                dbCharacter.Items.Add(dbItem);
                addedItems.Add((dbItem, itemDefinition));
            }
            else if (dbItem.Count < quantity)
            {
                dbItem.Count = quantity;
                changedItems.Add(dbItem);
            }
        }

        if (addedItems.Count + changedItems.Count == 0)
            return 0;

        if (dbContext.SaveChanges() <= 0)
            return 0;

        foreach (var (dbItem, itemDefinition) in addedItems)
        {
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
            var clientItem = player.Items.FirstOrDefault(x => x.Id == dbItem.Id);

            if (clientItem is null)
                continue;

            clientItem.Count = dbItem.Count;

            player.SendTunneled(new ClientUpdatePacketItemUpdate { ItemGuid = clientItem.Id, Count = clientItem.Count });
        }

        // Refresh the furniture list if they're standing in their own house.
        if (player.Zone is HousingZone housingZone && housingZone.IsOwner(player))
            housingZone.SendHouseData(player);

        return addedItems.Count + changedItems.Count;
    }

    public void SetHouseLocked(Player player, ulong houseGuid, bool isLocked)
    {
        var housingZone = _zoneManager.Zones
            .OfType<HousingZone>()
            .FirstOrDefault(x => x.OwnerId == houseGuid);

        if (housingZone is not null)
        {
            housingZone.SetLocked(player, isLocked);
            return;
        }

        if (!GuidHelper.IsHouseGuid(houseGuid))
            return;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var house = dbContext.Houses.SingleOrDefault(x => x.Id == GuidHelper.GetHouseId(houseGuid)
            && x.CharacterId == GuidHelper.GetPlayerId(player.Guid));

        if (house is null)
            return;

        house.IsLocked = isLocked;

        dbContext.SaveChanges();

        SendHouseList(player, player.Guid, player.Name.FullName);
    }
}
