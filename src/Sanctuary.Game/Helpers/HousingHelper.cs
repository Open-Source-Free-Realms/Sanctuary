using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class HousingHelper
{
    private const int ReservedHouseKeyBit = 30;
    private const int ReservedHouseKeyFlag = 1 << ReservedHouseKeyBit;

    public const int PreviewHouseKey = 0;

    private const int MaxFixtureCount = 9999;
    private const int MaxLandmarkCount = 100;

    public const string RatingSystem = "Housing";
    public const int MaxRatingEntryCount = 50;

    private const float DefaultRating = 3.0f;

    public static IEnumerable<HouseDefinition> GetStarterHouses(IResourceManager resourceManager) =>
        resourceManager.Houses.Values.Where(house => house.IsStarter).OrderBy(house => house.Id);

    public static IEnumerable<OwnedHouse> GetOwnedHouses(IResourceManager resourceManager, Player player)
    {
        var reservedHouses = resourceManager.Houses.Values
            .Where(house => house.IsStarter || IsStaffHouse(resourceManager, player, house))
            .Select(house => new OwnedHouse(GetReservedHouseKey(house), house));

        var boughtHouses = player.Items
            .Select(clientItem => TryGetItemHouseDefinition(resourceManager, clientItem.Definition, out var houseDefinition) ? new OwnedHouse(clientItem.Id, houseDefinition) : null)
            .OfType<OwnedHouse>();

        return reservedHouses.Concat(boughtHouses).OrderBy(house => house.Definition.Id).ThenBy(house => house.Key);
    }

    public static void NotifyHouseAdded(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, Player player, ClientItemDefinition clientItemDefinition, int itemId)
    {
        if (!TryGetItemHouseDefinition(resourceManager, clientItemDefinition.Id, out var houseDefinition))
            return;

        player.SendTunneled(new HousingPacketNotifyHouseAdded
        {
            NameId = houseDefinition.NameId,
            HouseGuid = GetHouseGuid(dbContextFactory, player.Guid, itemId, houseDefinition.ZoneId),
            OwnerGuid = player.Guid
        });

        player.SendTunneled(GetInstanceList(resourceManager, dbContextFactory, player));
    }

    public static bool TryGetHouseDefinition(IResourceManager resourceManager, int zoneDefinitionId, [MaybeNullWhen(false)] out HouseDefinition houseDefinition)
    {
        houseDefinition = resourceManager.Houses.Values.OrderBy(house => house.Id).FirstOrDefault(house => house.ZoneId == zoneDefinitionId);

        return houseDefinition is not null;
    }

    private static bool TryGetItemHouseDefinition(IResourceManager resourceManager, int itemDefinitionId, [MaybeNullWhen(false)] out HouseDefinition houseDefinition)
    {
        houseDefinition = resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var clientItemDefinition) && clientItemDefinition.Type == ItemDefinitionHelper.HouseItemType
            ? resourceManager.Houses.Values.OrderBy(house => house.Id).FirstOrDefault(house => house.NameId == clientItemDefinition.NameId)
            : null;

        return houseDefinition is not null;
    }

    public static int GetReservedHouseKey(HouseDefinition houseDefinition) => ReservedHouseKeyFlag | houseDefinition.Id;

    private static bool IsReservedHouseKey(int houseKey) => (houseKey & ReservedHouseKeyFlag) != 0;

    public static ulong GetHouseGuid(DbHouse dbHouse) => GuidHelper.GetHouseGuid(dbHouse.Id);

    public static ulong GetHouseGuid(IDbContextFactory<DatabaseContext> dbContextFactory, ulong ownerGuid, int houseKey, int zoneId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return GetHouseGuid(GetOrAddHouses(dbContext, GuidHelper.GetPlayerId(ownerGuid), [(houseKey, zoneId)])[houseKey]);
    }

    public static ulong GetHouseOwnerGuid(DbHouse dbHouse) => GuidHelper.GetPlayerGuid(dbHouse.CharacterId);

    private static Dictionary<int, DbHouse> GetOrAddHouses(DatabaseContext dbContext, ulong characterId, IEnumerable<(int Key, int ZoneId)> houses)
    {
        var dbHouses = dbContext.Houses.Include(house => house.Votes).Where(house => house.CharacterId == characterId).ToDictionary(house => house.Key);

        var newHouses = houses.Where(house => !dbHouses.ContainsKey(house.Key)).ToList();

        if (newHouses.Count == 0)
            return dbHouses;

        var dbHousingGuids = newHouses.Select(_ => new DbHousingGuid()).ToList();

        dbContext.HousingGuids.AddRange(dbHousingGuids);
        dbContext.SaveChanges();

        foreach (var ((houseKey, zoneId), dbHousingGuid) in newHouses.Zip(dbHousingGuids))
        {
            var dbHouse = new DbHouse
            {
                Id = dbHousingGuid.Id,
                Key = houseKey,
                ZoneId = zoneId,
                CharacterId = characterId
            };

            dbContext.Houses.Add(dbHouse);
            dbHouses.Add(houseKey, dbHouse);
        }

        dbContext.SaveChanges();

        return dbHouses;
    }

    public static bool TryGetHouse(IDbContextFactory<DatabaseContext> dbContextFactory, ulong houseGuid, [MaybeNullWhen(false)] out DbHouse dbHouse)
    {
        dbHouse = null;

        if (!GuidHelper.IsHouseGuid(houseGuid))
            return false;

        var houseId = GuidHelper.GetHouseId(houseGuid);

        using var dbContext = dbContextFactory.CreateDbContext();

        dbHouse = dbContext.Houses.AsNoTracking().FirstOrDefault(house => house.Id == houseId);

        return dbHouse is not null;
    }

    public static bool TryGetOwnedHouse(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, Player player, ulong houseGuid,
        [MaybeNullWhen(false)] out OwnedHouse ownedHouse)
    {
        ownedHouse = TryGetHouse(dbContextFactory, houseGuid, out var dbHouse) && GetHouseOwnerGuid(dbHouse) == player.Guid
            ? GetOwnedHouses(resourceManager, player).FirstOrDefault(house => house.Key == dbHouse.Key)
            : null;

        return ownedHouse is not null;
    }

    public static bool CanEnterHouseZone(IResourceManager resourceManager, Player player, int zoneDefinitionId) =>
        resourceManager.Zones.TryGetValue(zoneDefinitionId, out var zoneDefinition)
        && zoneDefinition is HousingZoneDefinition { IsBroken: false } housingZoneDefinition
        && (!housingZoneDefinition.StaffOnly || IsStaff(player));

    private static bool IsStaff(Player player) => player.IsAdmin || player.IsMod;

    private static bool IsStaffHouse(IResourceManager resourceManager, Player player, HouseDefinition houseDefinition) =>
        IsStaff(player) && IsStaffOnlyHouse(resourceManager, houseDefinition);

    public static bool IsStaffOnlyHouse(IResourceManager resourceManager, HouseDefinition houseDefinition) =>
        resourceManager.Zones.TryGetValue(houseDefinition.ZoneId, out var zoneDefinition) && zoneDefinition is HousingZoneDefinition { StaffOnly: true };

    private static bool TryGetHouseDefinition(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager,
        Player player, DbHouse dbHouse, [MaybeNullWhen(false)] out HouseDefinition houseDefinition)
    {
        houseDefinition = null;

        var ownerGuid = GetHouseOwnerGuid(dbHouse);
        var houseKey = dbHouse.Key;

        var owner = ownerGuid == player.Guid ? player : zoneManager.TryGetPlayer(ownerGuid, out var onlineOwner) ? onlineOwner : null;

        if (owner is not null)
        {
            houseDefinition = GetOwnedHouses(resourceManager, owner).FirstOrDefault(house => house.Key == houseKey)?.Definition;
            return houseDefinition is not null;
        }

        if (IsReservedHouseKey(houseKey))
        {
            houseDefinition = resourceManager.Houses.TryGetValue(houseKey & ~ReservedHouseKeyFlag, out var reservedHouse)
                && (reservedHouse.IsStarter || IsStaffOnlyHouse(resourceManager, reservedHouse))
                ? reservedHouse
                : null;

            return houseDefinition is not null;
        }

        using var dbContext = dbContextFactory.CreateDbContext();

        var itemDefinitionId = dbContext.Items.AsNoTracking()
            .Where(item => item.Id == houseKey && item.CharacterId == dbHouse.CharacterId)
            .Select(item => (int?)item.Definition)
            .FirstOrDefault();

        return itemDefinitionId is { } houseItemDefinitionId && TryGetItemHouseDefinition(resourceManager, houseItemDefinitionId, out houseDefinition);
    }

    public static bool TryEnterHouse(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager,
        Player player, ulong houseGuid, out HousingError housingError)
    {
        if (!TryGetHouse(dbContextFactory, houseGuid, out var dbHouse)
            || !TryGetHouseDefinition(resourceManager, dbContextFactory, zoneManager, player, dbHouse, out var houseDefinition)
            || !CanEnterHouseZone(resourceManager, player, houseDefinition.ZoneId)
            || !resourceManager.Zones.TryGetValue(houseDefinition.ZoneId, out var zoneDefinition))
        {
            housingError = HousingError.ZoneNotFound;
            return false;
        }

        if (GetHouseOwnerGuid(dbHouse) != player.Guid && !player.HouseInvites.ContainsKey(houseGuid) && dbHouse.IsLocked && !dbHouse.IsPublished)
        {
            housingError = HousingError.PermissionDenied;
            return false;
        }

        player.HouseInvites.Remove(houseGuid);

        var isTeleported = player.TeleportToZone(zoneDefinition.Id, houseGuid, zoneDefinition.SpawnPosition, zoneDefinition.SpawnRotation);

        player.IsPreviewingHouse = false;

        if (!isTeleported)
        {
            housingError = HousingError.ZoneFailedToLaunch;
            return false;
        }

        housingError = default;
        return true;
    }

    public static bool TryPreviewHouse(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, Player player, int itemDefinitionId, out HousingError housingError)
    {
        if (!TryGetItemHouseDefinition(resourceManager, itemDefinitionId, out var houseDefinition)
            || !CanEnterHouseZone(resourceManager, player, houseDefinition.ZoneId)
            || !resourceManager.Zones.TryGetValue(houseDefinition.ZoneId, out var zoneDefinition))
        {
            housingError = HousingError.ZoneNotFound;
            return false;
        }

        player.IsPreviewingHouse = true;

        if (!player.TeleportToZone(zoneDefinition.Id, GetHouseGuid(dbContextFactory, player.Guid, PreviewHouseKey, zoneDefinition.Id), zoneDefinition.SpawnPosition, zoneDefinition.SpawnRotation))
        {
            player.IsPreviewingHouse = false;
            housingError = HousingError.ZoneFailedToLaunch;
            return false;
        }

        housingError = default;
        return true;
    }

    public static bool TryGetOwnedZone(Player player, [MaybeNullWhen(false)] out HousingZone housingZone)
    {
        housingZone = player.Zone as HousingZone;

        return housingZone is not null && housingZone.IsOwner(player);
    }

    public static bool TryLeaveHouse(Player player, IZoneManager zoneManager)
    {
        if (player.Zone is not HousingZone)
            return false;

        player.IsPreviewingHouse = false;

        return player.TeleportToZone(zoneManager.StartingZoneDefinitionId, null, player.StartingZonePosition, player.StartingZoneRotation);
    }

    public static PlayerHousingInstanceData GetInstanceData(ulong houseGuid, ulong ownerGuid, string ownerName, HouseDefinition houseDefinition) => new()
    {
        HouseGuid = houseGuid,
        OwnerGuid = ownerGuid,
        OwnerName = ownerName,
        NameId = houseDefinition.NameId,
        IconId = houseDefinition.Icon,
        MaxFixtureCount = MaxFixtureCount,
        MaxLandmarkCount = MaxLandmarkCount,
        IsFloraAllowed = true,
        BuildAreas = houseDefinition.BuildAreas
    };

    public static PlayerHousingInstanceInfo GetInstanceInfo(ulong houseGuid, ulong ownerGuid, string ownerName, HouseDefinition houseDefinition) => new()
    {
        OwnerGuid = ownerGuid,
        InstanceGuid = houseGuid,
        NameId = houseDefinition.NameId,
        OwnerName = ownerName,
        IconId = houseDefinition.Icon,
        LastVisited = DateTimeOffset.UtcNow,
        WhenCreated = DateTimeOffset.UtcNow
    };

    public static HousingPacketInstanceList GetInstanceList(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, Player player)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = dbContextFactory.CreateDbContext();

        var characterCreated = dbContext.Characters.AsNoTracking().Where(character => character.Id == characterId).Select(character => character.Created).FirstOrDefault();

        var itemCreated = dbContext.Items.AsNoTracking().Where(item => item.CharacterId == characterId).ToDictionary(item => item.Id, item => item.Created);

        var ownedHouses = GetOwnedHouses(resourceManager, player).ToList();

        var dbHouses = GetOrAddHouses(dbContext, characterId, ownedHouses.Select(house => (house.Key, house.Definition.ZoneId)));

        return new HousingPacketInstanceList
        {
            PlayerGuid = player.Guid,
            Instances = ownedHouses.Select(house =>
            {
                var dbHouse = dbHouses[house.Key];

                var instanceInfo = GetInstanceInfo(GetHouseGuid(dbHouse), player.Guid, player.Name.FullName, house.Definition);

                instanceInfo.WhenCreated = !IsReservedHouseKey(house.Key) && itemCreated.TryGetValue(house.Key, out var houseCreated) ? houseCreated : characterCreated;

                instanceInfo.HouseName = dbHouse.Name;
                instanceInfo.IsLocked = dbHouse.IsLocked;
                instanceInfo.IsFloraAllowed = dbHouse.IsFloraAllowed;

                SetRatingInfo(instanceInfo, dbHouse, player.Guid);

                return instanceInfo;
            }).ToList()
        };
    }

    public static string GetRatingHouseGuid(ulong houseGuid) => houseGuid.ToString(CultureInfo.InvariantCulture);

    public static IQueryable<DbHouse> GetPublishedHouses(DatabaseContext dbContext) =>
        dbContext.Houses.Include(house => house.Votes).Include(house => house.Character).Where(house => house.IsPublished);

    public static bool TryGetHouse(DatabaseContext dbContext, string ratingHouseGuid,
        out ulong houseGuid, [MaybeNullWhen(false)] out DbHouse dbHouse)
    {
        dbHouse = null;

        if (!ulong.TryParse(ratingHouseGuid, CultureInfo.InvariantCulture, out houseGuid) || !GuidHelper.IsHouseGuid(houseGuid))
            return false;

        var houseId = GuidHelper.GetHouseId(houseGuid);

        dbHouse = dbContext.Houses.Include(house => house.Votes).Include(house => house.Character)
            .FirstOrDefault(house => house.Id == houseId);

        return dbHouse is not null;
    }

    public static float GetRating(DbHouse dbHouse) => dbHouse.Votes.Count == 0 ? DefaultRating : dbHouse.Votes.Average(vote => vote.Rating);

    public static bool CanVote(DbHouse dbHouse, ulong voterGuid)
    {
        var voterId = GuidHelper.GetPlayerId(voterGuid);

        return dbHouse.IsPublished && dbHouse.CharacterId != voterId && dbHouse.Votes.All(vote => vote.VoterId != voterId);
    }

    public static CandidateRatingInfo GetCandidateRatingInfo(ulong houseGuid, DbHouse dbHouse, ulong requesterGuid) => new()
    {
        InstanceGuid = GetRatingHouseGuid(houseGuid),
        Description = dbHouse.Description,
        KeywordList = dbHouse.Keywords,
        Rating = GetRating(dbHouse),
        Votes = dbHouse.Votes.Count,
        HasRating = dbHouse.IsPublished,
        CanVote = CanVote(dbHouse, requesterGuid)
    };

    public static RatingDataEntry GetRatingDataEntry(ulong houseGuid, DbHouse dbHouse) => new()
    {
        HouseGuid = GetRatingHouseGuid(houseGuid),
        OwnerName = dbHouse.Character.FullName,
        HouseName = dbHouse.Name,
        OwnerGuid = GuidHelper.GetPlayerGuid(dbHouse.CharacterId),
        Description = dbHouse.Description,
        KeywordList = dbHouse.Keywords,
        Rating = GetRating(dbHouse),
        NumVotes = dbHouse.Votes.Count
    };

    public static List<RatingDataEntry> GetRatingDataEntries(IResourceManager resourceManager, IEnumerable<DbHouse> dbHouses)
    {
        var ratingDataEntries = new List<RatingDataEntry>();

        foreach (var dbHouse in dbHouses)
        {
            if (!TryGetHouseDefinition(resourceManager, dbHouse.ZoneId, out var houseDefinition))
                continue;

            var ratingDataEntry = GetRatingDataEntry(GetHouseGuid(dbHouse), dbHouse);

            ratingDataEntry.Snapshot = houseDefinition.Snapshot;

            ratingDataEntries.Add(ratingDataEntry);
        }

        return ratingDataEntries;
    }

    public static void SendCandidateInfo(Player player, ulong houseGuid, DbHouse dbHouse)
    {
        var players = player.Zone is HousingZone { OwnerId: { } zoneHouseGuid } housingZone && zoneHouseGuid == houseGuid
            ? housingZone.Players
            : [player];

        foreach (var zonePlayer in players)
        {
            zonePlayer.SendTunneled(new RatingPacketCandidateInfoReply
            {
                Guid = zonePlayer.Guid,
                Candidates = [GetCandidateRatingInfo(houseGuid, dbHouse, zonePlayer.Guid)]
            });
        }
    }

    public static void SetRatingInfo(PlayerHousingInstanceInfo instanceInfo, DbHouse dbHouse, ulong requesterGuid)
    {
        instanceInfo.Description = dbHouse.Description;
        instanceInfo.KeywordList = dbHouse.Keywords;
        instanceInfo.Rating = GetRating(dbHouse);
        instanceInfo.Votes = dbHouse.Votes.Count;
        instanceInfo.HasRating = dbHouse.IsPublished;
        instanceInfo.CanVote = CanVote(dbHouse, requesterGuid);
    }
}
