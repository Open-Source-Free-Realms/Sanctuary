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
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class HousingHelper
{
    private const int HouseDefinitionIdBits = 8;
    private const ulong HouseDefinitionIdMask = (1UL << HouseDefinitionIdBits) - 1;

    private const int HouseItemType = 16;

    private const int MaxFixtureCount = 9999;
    private const int MaxLandmarkCount = 100;

    public const string RatingSystem = "Housing";
    public const int MaxRatingEntryCount = 50;

    private const float DefaultRating = 3.0f;

    public static IEnumerable<HouseDefinition> GetStarterHouses(IResourceManager resourceManager) =>
        resourceManager.Houses.Values.Where(house => house.IsStarter).OrderBy(house => house.Id);

    public static IEnumerable<HouseDefinition> GetOwnedHouses(IResourceManager resourceManager, Player player) =>
        player.ChatCommandRole == ChatCommandRole.Player
            ? GetStarterHouses(resourceManager)
            : resourceManager.Houses.Values.Where(house => CanEnterHouseZone(resourceManager, player, house.ZoneId)).OrderBy(house => house.Id);

    public static bool TryGetHouseDefinition(IResourceManager resourceManager, int zoneDefinitionId, [MaybeNullWhen(false)] out HouseDefinition houseDefinition)
    {
        houseDefinition = resourceManager.Houses.Values.OrderBy(house => house.Id).FirstOrDefault(house => house.ZoneId == zoneDefinitionId);

        return houseDefinition is not null;
    }

    public static ulong GetHouseGuid(ulong ownerGuid, HouseDefinition houseDefinition) =>
        (ownerGuid << HouseDefinitionIdBits) | (uint)houseDefinition.Id;

    public static bool TryGetOwnedHouse(IResourceManager resourceManager, Player player, ulong houseGuid,
        [MaybeNullWhen(false)] out HouseDefinition houseDefinition)
    {
        var houseDefinitionId = (int)(houseGuid & HouseDefinitionIdMask);

        houseDefinition = houseGuid >> HouseDefinitionIdBits == player.Guid
            ? GetOwnedHouses(resourceManager, player).FirstOrDefault(house => house.Id == houseDefinitionId)
            : null;

        return houseDefinition is not null;
    }

    public static bool CanEnterHouseZone(IResourceManager resourceManager, Player player, int zoneDefinitionId) =>
        resourceManager.Zones.TryGetValue(zoneDefinitionId, out var zoneDefinition)
        && zoneDefinition is HousingZoneDefinition { IsBroken: false } housingZoneDefinition
        && (!housingZoneDefinition.StaffOnly || player.ChatCommandRole != ChatCommandRole.Player);

    public static ulong GetHouseOwnerGuid(ulong houseGuid) => houseGuid >> HouseDefinitionIdBits;

    public static bool TryEnterHouse(IResourceManager resourceManager, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager,
        Player player, ulong houseGuid, out HousingError housingError)
    {
        var ownerGuid = GetHouseOwnerGuid(houseGuid);
        var houseDefinitionId = (int)(houseGuid & HouseDefinitionIdMask);

        var owner = ownerGuid == player.Guid ? player : zoneManager.TryGetPlayer(ownerGuid, out var onlineOwner) ? onlineOwner : null;
        var houseDefinition = (owner is null ? GetStarterHouses(resourceManager) : GetOwnedHouses(resourceManager, owner)).FirstOrDefault(house => house.Id == houseDefinitionId);

        if (houseDefinition is null || !CanEnterHouseZone(resourceManager, player, houseDefinition.ZoneId)
            || !resourceManager.Zones.TryGetValue(houseDefinition.ZoneId, out var zoneDefinition))
        {
            housingError = HousingError.ZoneNotFound;
            return false;
        }

        if (ownerGuid != player.Guid && !player.HouseInvites.Contains(houseGuid) && IsHouseLocked(dbContextFactory, ownerGuid, houseDefinition.ZoneId))
        {
            housingError = HousingError.PermissionDenied;
            return false;
        }

        player.HouseInvites.Remove(houseGuid);
        player.IsPreviewingHouse = false;

        if (!player.TeleportToZone(zoneDefinition.Id, ownerGuid, zoneDefinition.SpawnPosition, zoneDefinition.SpawnRotation))
        {
            housingError = HousingError.ZoneFailedToLaunch;
            return false;
        }

        housingError = default;
        return true;
    }

    public static bool TryPreviewHouse(IResourceManager resourceManager, Player player, int itemDefinitionId, out HousingError housingError)
    {
        var houseDefinition = resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var clientItemDefinition) && clientItemDefinition.Type == HouseItemType
            ? resourceManager.Houses.Values.OrderBy(house => house.Id).FirstOrDefault(house => house.NameId == clientItemDefinition.NameId)
            : null;

        if (houseDefinition is null || !CanEnterHouseZone(resourceManager, player, houseDefinition.ZoneId)
            || !resourceManager.Zones.TryGetValue(houseDefinition.ZoneId, out var zoneDefinition))
        {
            housingError = HousingError.ZoneNotFound;
            return false;
        }

        player.IsPreviewingHouse = true;

        if (!player.TeleportToZone(zoneDefinition.Id, player.Guid, zoneDefinition.SpawnPosition, zoneDefinition.SpawnRotation))
        {
            player.IsPreviewingHouse = false;
            housingError = HousingError.ZoneFailedToLaunch;
            return false;
        }

        housingError = default;
        return true;
    }

    public static bool IsHouseLocked(IDbContextFactory<DatabaseContext> dbContextFactory, ulong ownerGuid, int zoneDefinitionId)
    {
        var characterId = GuidHelper.GetPlayerId(ownerGuid);

        using var dbContext = dbContextFactory.CreateDbContext();

        return dbContext.Houses.AsNoTracking().Any(house => house.Id == zoneDefinitionId && house.CharacterId == characterId && house.IsLocked && !house.IsPublished);
    }

    public static bool TryLeaveHouse(Player player, IZoneManager zoneManager)
    {
        if (player.Zone is not HousingZone)
            return false;

        player.IsPreviewingHouse = false;

        return player.TeleportToZone(zoneManager.StartingZoneDefinitionId, null, player.StartingZonePosition, player.StartingZoneRotation);
    }

    public static PlayerHousingInstanceData GetInstanceData(ulong ownerGuid, string ownerName, HouseDefinition houseDefinition) => new()
    {
        HouseGuid = GetHouseGuid(ownerGuid, houseDefinition),
        OwnerGuid = ownerGuid,
        OwnerName = ownerName,
        NameId = houseDefinition.NameId,
        IconId = houseDefinition.Icon,
        MaxFixtureCount = MaxFixtureCount,
        MaxLandmarkCount = MaxLandmarkCount,
        IsFloraAllowed = true,
        BuildAreas = houseDefinition.BuildAreas
    };

    public static PlayerHousingInstanceInfo GetInstanceInfo(ulong ownerGuid, string ownerName, HouseDefinition houseDefinition) => new()
    {
        OwnerGuid = ownerGuid,
        InstanceGuid = GetHouseGuid(ownerGuid, houseDefinition),
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

        var dbHouses = dbContext.Houses.AsNoTracking().Include(house => house.Votes).Where(house => house.CharacterId == characterId).ToDictionary(house => house.Id);

        return new HousingPacketInstanceList
        {
            PlayerGuid = player.Guid,
            Instances = GetOwnedHouses(resourceManager, player).Select(house =>
            {
                var instanceInfo = GetInstanceInfo(player.Guid, player.Name.FullName, house);

                if (dbHouses.TryGetValue(house.ZoneId, out var dbHouse))
                {
                    instanceInfo.HouseName = dbHouse.Name;
                    instanceInfo.IsLocked = dbHouse.IsLocked;
                    instanceInfo.IsFloraAllowed = dbHouse.IsFloraAllowed;

                    SetRatingInfo(instanceInfo, dbHouse, player.Guid);
                }

                return instanceInfo;
            }).ToList()
        };
    }

    public static string GetRatingHouseGuid(ulong houseGuid) => houseGuid.ToString(CultureInfo.InvariantCulture);

    public static IQueryable<DbHouse> GetPublishedHouses(DatabaseContext dbContext) =>
        dbContext.Houses.Include(house => house.Votes).Include(house => house.Character).Where(house => house.IsPublished);

    public static bool TryGetHouse(IResourceManager resourceManager, DatabaseContext dbContext, string ratingHouseGuid,
        out ulong houseGuid, [MaybeNullWhen(false)] out DbHouse dbHouse)
    {
        dbHouse = null;

        if (!ulong.TryParse(ratingHouseGuid, CultureInfo.InvariantCulture, out houseGuid))
            return false;

        var houseDefinitionId = (int)(houseGuid & HouseDefinitionIdMask);

        var houseDefinition = resourceManager.Houses.Values.FirstOrDefault(house => house.Id == houseDefinitionId);

        if (houseDefinition is null)
            return false;

        var characterId = GuidHelper.GetPlayerId(GetHouseOwnerGuid(houseGuid));

        dbHouse = dbContext.Houses.Include(house => house.Votes).Include(house => house.Character)
            .FirstOrDefault(house => house.Id == houseDefinition.ZoneId && house.CharacterId == characterId);

        return dbHouse is not null;
    }

    public static bool TryGetHouseGuid(IResourceManager resourceManager, DbHouse dbHouse, out ulong houseGuid)
    {
        if (!TryGetHouseDefinition(resourceManager, dbHouse.Id, out var houseDefinition))
        {
            houseGuid = default;
            return false;
        }

        houseGuid = GetHouseGuid(GuidHelper.GetPlayerGuid(dbHouse.CharacterId), houseDefinition);
        return true;
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
            if (!TryGetHouseGuid(resourceManager, dbHouse, out var houseGuid))
                continue;

            var ratingDataEntry = GetRatingDataEntry(houseGuid, dbHouse);

            if (TryGetHouseDefinition(resourceManager, dbHouse.Id, out var houseDefinition))
                ratingDataEntry.Snapshot = houseDefinition.Snapshot;

            ratingDataEntries.Add(ratingDataEntry);
        }

        return ratingDataEntries;
    }

    public static void SendCandidateInfo(Player player, ulong houseGuid, DbHouse dbHouse)
    {
        var players = player.Zone is HousingZone { OwnerId: { } ownerGuid } housingZone && ownerGuid == GetHouseOwnerGuid(houseGuid) && housingZone.DefinitionId == dbHouse.Id
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
