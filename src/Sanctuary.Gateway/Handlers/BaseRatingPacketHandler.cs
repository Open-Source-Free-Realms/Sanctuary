using System;
using System.Data;
using System.Globalization;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseRatingPacketHandler
{
    private const string HousingSystem = "Housing";
    private const int MaxDirectoryEntries = 50;

    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseRatingPacketHandler));
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out byte subOpCode))
            return false;

        var payload = reader.RemainingSpan;
        return subOpCode switch
        {
            3 => HandleDataRequest(connection, payload),
            6 => HandlePublish(connection),
            7 => HandleUnpublish(connection),
            8 => HandleVote(connection, payload),
            12 => HandleSearch(connection, payload),
            16 => HandleCandidateInfo(connection, payload),
            20 => HandleFeatured(connection, payload),
            _ => false
        };
    }

    private static bool HandleDataRequest(GatewayConnection connection, ReadOnlySpan<byte> payload)
    {
        var reader = new PacketReader(payload);
        if (!reader.TryRead(out string system) || !reader.TryRead(out int mode))
        {
            LogMalformed(connection, 3, payload);
            return false;
        }

        if (!IsHousingSystem(system))
            return false;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var query = CreateDirectoryQuery(dbContext, connection.Player.MembershipStatus != 0);

        if (mode == 2)
        {
            query = query.Where(house => dbContext.Friends.Any(friend =>
                friend.CharacterId == characterId &&
                friend.FriendCharacterId == house.CharacterId));
        }

        var totalCount = query.Count();
        var selected = SortDirectoryQuery(query, mode)
            .Take(MaxDirectoryEntries)
            .ToList();
        var response = new RatingPacketDataReply
        {
            Correlation = connection.Player.Guid,
            System = HousingSystem,
            TotalCount = totalCount
        };

        for (int index = 0; index < selected.Count; index++)
            response.Entries[index] = ToRatingEntry(selected[index]);

        connection.SendTunneled(response);
        return true;
    }

    private static bool HandleSearch(GatewayConnection connection, ReadOnlySpan<byte> payload)
    {
        var reader = new PacketReader(payload);
        if (!reader.TryRead(out ulong correlation) ||
            !reader.TryRead(out string system) ||
            !reader.TryRead(out string searchText))
        {
            LogMalformed(connection, 12, payload);
            return false;
        }

        if (!IsHousingSystem(system))
            return false;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var query = CreateDirectoryQuery(dbContext, connection.Player.MembershipStatus != 0);
        var normalizedSearch = searchText.Trim().ToLowerInvariant();

        if (normalizedSearch.Length > 0)
        {
            var matchingDefinitionIds = GetSupportedDefinitions()
                .Where(definition => definition.DisplayName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .Select(definition => definition.Id)
                .ToArray();

            query = query.Where(house =>
                matchingDefinitionIds.Contains(house.ZoneDefinitionId) ||
                house.Name != null && house.Name.ToLower().Contains(normalizedSearch) ||
                house.Description.ToLower().Contains(normalizedSearch) ||
                house.KeywordList.ToLower().Contains(normalizedSearch) ||
                house.Character.FullName != null && house.Character.FullName.ToLower().Contains(normalizedSearch) ||
                house.Character.FirstName.ToLower().Contains(normalizedSearch) ||
                house.Character.LastName != null && house.Character.LastName.ToLower().Contains(normalizedSearch));
        }

        var houses = SortDirectoryQuery(query, 0)
            .Take(MaxDirectoryEntries)
            .ToList();

        connection.SendTunneled(new RatingPacketSearchReply
        {
            Correlation = correlation,
            Query = searchText,
            Entries = houses.Select(ToRatingEntry).ToList()
        });
        return true;
    }

    private static bool HandleFeatured(GatewayConnection connection, ReadOnlySpan<byte> payload)
    {
        var reader = new PacketReader(payload);
        if (!reader.TryRead(out string system) || !reader.TryRead(out ulong correlation))
        {
            LogMalformed(connection, 20, payload);
            return false;
        }

        if (!IsHousingSystem(system))
            return false;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var house = SortDirectoryQuery(
            CreateDirectoryQuery(dbContext, connection.Player.MembershipStatus != 0),
            0).FirstOrDefault();

        connection.SendTunneled(new RatingPacketSendFeatured
        {
            Correlation = correlation,
            System = HousingSystem,
            Entry = house is null ? new RatingDataEntry() : ToRatingEntry(house)
        });
        return true;
    }

    private static bool HandleCandidateInfo(GatewayConnection connection, ReadOnlySpan<byte> payload)
    {
        var reader = new PacketReader(payload);
        if (!reader.TryRead(out string system) ||
            !reader.TryRead(out string candidateId) ||
            !reader.TryRead(out ulong ownerGuid) ||
            !reader.TryRead(out ulong correlation))
        {
            LogMalformed(connection, 16, payload);
            return false;
        }

        if (!IsHousingSystem(system))
            return false;

        using var dbContext = _dbContextFactory.CreateDbContext();
        var house = FindCandidateHouse(dbContext, connection, candidateId, ownerGuid);
        SendCandidateInfo(connection, dbContext, house, correlation);
        return true;
    }

    private static bool HandlePublish(GatewayConnection connection)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var house = FindCurrentOwnedHouse(dbContext, connection);
        if (house is null)
            return true;

        house.IsPublished = true;
        dbContext.SaveChanges();
        SendCandidateInfo(connection, dbContext, house, connection.Player.Guid);
        return true;
    }

    private static bool HandleUnpublish(GatewayConnection connection)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var house = FindCurrentOwnedHouse(dbContext, connection);
        if (house is null)
            return true;

        house.IsPublished = false;
        dbContext.SaveChanges();
        SendCandidateInfo(connection, dbContext, house, connection.Player.Guid);
        return true;
    }

    private static bool HandleVote(GatewayConnection connection, ReadOnlySpan<byte> payload)
    {
        var reader = new PacketReader(payload);
        var values = new string[4];

        for (int index = 0; index < values.Length; index++)
        {
            if (!reader.TryRead(out values[index]))
            {
                LogMalformed(connection, 8, payload);
                return false;
            }
        }

        var rating = ParseVote(values);
        var houseId = TryAddVote(connection, rating);

        connection.SendTunneled(new RatingPacketVoteReply());

        if (houseId is not null)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var house = dbContext.Houses
                .AsNoTracking()
                .Include(entry => entry.Character)
                .SingleOrDefault(entry => entry.Id == houseId.Value);
            SendCandidateInfo(connection, dbContext, house, connection.Player.Guid);
        }

        return true;
    }

    private static ulong? TryAddVote(GatewayConnection connection, int rating)
    {
        if (rating is < 1 or > 5 ||
            !TryGetActiveHouseKey(connection, out var houseId, out var ownerId, out var zoneDefinitionId))
        {
            return null;
        }

        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        using var strategyContext = _dbContextFactory.CreateDbContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        try
        {
            return strategy.Execute(() =>
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                using var transaction = dbContext.Database.BeginTransaction(IsolationLevel.Serializable);
                var house = dbContext.Houses.SingleOrDefault(entry =>
                    entry.Id == houseId &&
                    entry.CharacterId == ownerId &&
                    entry.ZoneDefinitionId == zoneDefinitionId);

                if (house is null || !house.IsPublished || house.CharacterId == characterId)
                    return (ulong?)null;

                if (dbContext.HouseVotes.Any(vote =>
                    vote.HouseId == house.Id &&
                    vote.CharacterId == characterId))
                {
                    return (ulong?)house.Id;
                }

                dbContext.HouseVotes.Add(new DbHouseVote
                {
                    HouseId = house.Id,
                    CharacterId = characterId,
                    Value = rating
                });
                dbContext.SaveChanges();

                var aggregate = dbContext.HouseVotes
                    .Where(vote => vote.HouseId == house.Id)
                    .GroupBy(vote => vote.HouseId)
                    .Select(group => new
                    {
                        Votes = group.Count(),
                        Rating = group.Average(vote => vote.Value)
                    })
                    .Single();

                house.Votes = aggregate.Votes;
                house.Rating = (float)aggregate.Rating;
                dbContext.SaveChanges();
                transaction.Commit();
                return (ulong?)house.Id;
            });
        }
        catch (DbUpdateException exception)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var existingHouseId = dbContext.Houses
                .Where(house =>
                    house.Id == houseId &&
                    house.CharacterId == ownerId &&
                    house.ZoneDefinitionId == zoneDefinitionId)
                .Select(house => (ulong?)house.Id)
                .SingleOrDefault();

            if (existingHouseId is not null && dbContext.HouseVotes.Any(vote =>
                vote.HouseId == existingHouseId.Value &&
                vote.CharacterId == characterId))
            {
                return existingHouseId;
            }

            _logger.LogWarning(exception, "Failed to add a rating for the active house.");
            return null;
        }
    }

    private static IQueryable<DbHouse> CreateDirectoryQuery(DatabaseContext dbContext, bool isMember)
    {
        var supportedZoneDefinitionIds = GetSupportedDefinitions()
            .Select(definition => definition.Id)
            .ToArray();

        return dbContext.Houses
            .AsNoTracking()
            .Include(house => house.Character)
            .Where(house =>
                supportedZoneDefinitionIds.Contains(house.ZoneDefinitionId) &&
                house.IsPublished &&
                !house.IsLocked &&
                (!house.IsMembersOnly || isMember));
    }

    private static IOrderedQueryable<DbHouse> SortDirectoryQuery(IQueryable<DbHouse> query, int mode)
    {
        return mode == 3
            ? query.OrderByDescending(house => house.Id)
            : query
                .OrderByDescending(house => house.Rating)
                .ThenByDescending(house => house.Votes)
                .ThenByDescending(house => house.Id);
    }

    private static DbHouse? FindCandidateHouse(
        DatabaseContext dbContext,
        GatewayConnection connection,
        string candidateId,
        ulong ownerGuid)
    {
        var supportedZoneDefinitionIds = GetSupportedDefinitions()
            .Select(definition => definition.Id)
            .ToArray();
        var query = dbContext.Houses
            .AsNoTracking()
            .Include(house => house.Character)
            .Where(house => supportedZoneDefinitionIds.Contains(house.ZoneDefinitionId));

        if (TryParseHouseId(candidateId, out var houseId))
        {
            var candidate = query.SingleOrDefault(house => house.Id == houseId);
            if (candidate is not null && CanViewCandidate(connection, candidate))
                return candidate;
        }

        if (TryGetActiveHouseKey(
            connection,
            out var activeHouseId,
            out var activeOwnerId,
            out var activeZoneDefinitionId))
        {
            var current = query.SingleOrDefault(house =>
                house.Id == activeHouseId &&
                house.CharacterId == activeOwnerId &&
                house.ZoneDefinitionId == activeZoneDefinitionId);
            if (current is not null)
                return current;
        }

        if (!TryGetPlayerId(ownerGuid, out var ownerId))
            return null;

        var ownedCandidate = query
            .Where(house => house.CharacterId == ownerId)
            .OrderBy(house => house.Id)
            .FirstOrDefault();
        return ownedCandidate is not null && CanViewCandidate(connection, ownedCandidate)
            ? ownedCandidate
            : null;
    }

    private static bool CanViewCandidate(GatewayConnection connection, DbHouse house)
    {
        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        if (house.CharacterId == characterId)
            return true;

        if (TryGetActiveHouseKey(
                connection,
                out var activeHouseId,
                out var activeOwnerId,
                out var activeZoneDefinitionId) &&
            activeHouseId == house.Id &&
            activeOwnerId == house.CharacterId &&
            activeZoneDefinitionId == house.ZoneDefinitionId)
        {
            return true;
        }

        if (house.IsMembersOnly && connection.Player.MembershipStatus == 0)
            return false;

        var isFriend = connection.Player.Friends.Any(friend =>
            friend.Guid == GuidHelper.GetPlayerGuid(house.CharacterId));
        return isFriend || house.IsPublished && !house.IsLocked;
    }

    private static DbHouse? FindCurrentOwnedHouse(DatabaseContext dbContext, GatewayConnection connection)
    {
        if (!TryGetActiveHouseKey(connection, out var houseId, out var ownerId, out var zoneDefinitionId))
            return null;

        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        if (characterId != ownerId)
            return null;

        return dbContext.Houses
            .Include(house => house.Character)
            .SingleOrDefault(house =>
                house.Id == houseId &&
                house.CharacterId == characterId &&
                house.ZoneDefinitionId == zoneDefinitionId);
    }

    private static void SendCandidateInfo(
        GatewayConnection connection,
        DatabaseContext dbContext,
        DbHouse? house,
        ulong correlation)
    {
        var response = new RatingPacketCandidateInfoReply { Correlation = correlation };

        if (house is not null && IsSupportedZoneDefinition(house.ZoneDefinitionId))
        {
            var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
            var isActiveHouse = TryGetActiveHouseKey(
                    connection,
                    out var activeHouseId,
                    out var ownerId,
                    out var zoneDefinitionId) &&
                activeHouseId == house.Id &&
                ownerId == house.CharacterId &&
                zoneDefinitionId == house.ZoneDefinitionId;

            response.Candidates.Add(new CandidateRatingInfo
            {
                CandidateId = GetCandidateId(house),
                OwnerName = GetCharacterName(house.Character),
                Name = GetHouseName(house),
                Rating = house.Rating,
                Votes = house.Votes,
                HasRating = house.IsPublished,
                CanVote = isActiveHouse &&
                    house.IsPublished &&
                    house.CharacterId != characterId &&
                    !dbContext.HouseVotes.Any(vote =>
                        vote.HouseId == house.Id &&
                        vote.CharacterId == characterId)
            });
        }

        connection.SendTunneled(response);
    }

    private static RatingDataEntry ToRatingEntry(DbHouse house)
    {
        var definition = GetSupportedDefinitions()
            .Single(entry => entry.Id == house.ZoneDefinitionId);

        return new RatingDataEntry
        {
            CandidateId = GetCandidateId(house),
            OwnerName = GetCharacterName(house.Character),
            Name = GetHouseName(house),
            OwnerGuid = GuidHelper.GetPlayerGuid(house.CharacterId),
            Snapshot = definition.DirectorySnapshot,
            Description = house.Description,
            Keywords = house.KeywordList,
            Rating = house.Rating,
            Votes = house.Votes
        };
    }

    private static string GetCandidateId(DbHouse house)
    {
        return GuidHelper.GetHouseGuid(house.Id).ToString(CultureInfo.InvariantCulture);
    }

    private static string GetHouseName(DbHouse house)
    {
        if (!string.IsNullOrWhiteSpace(house.Name))
            return house.Name;

        return GetSupportedDefinitions()
            .Single(definition => definition.Id == house.ZoneDefinitionId)
            .DisplayName;
    }

    private static string GetCharacterName(DbCharacter character)
    {
        if (!string.IsNullOrWhiteSpace(character.FullName))
            return character.FullName;

        return string.IsNullOrWhiteSpace(character.LastName)
            ? character.FirstName
            : $"{character.FirstName} {character.LastName}";
    }

    private static int ParseVote(string[] values)
    {
        for (int index = values.Length - 1; index >= 0; index--)
        {
            if (int.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating) &&
                rating is >= 1 and <= 5)
            {
                return rating;
            }
        }

        return 0;
    }

    private static bool TryParseHouseId(string value, out ulong houseId)
    {
        houseId = 0;
        if (!ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var houseGuid))
            return false;

        try
        {
            houseId = GuidHelper.GetHouseId(houseGuid);
            return houseId > 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryGetPlayerId(ulong playerGuid, out ulong playerId)
    {
        playerId = 0;
        if (playerGuid == 0)
            return false;

        try
        {
            playerId = GuidHelper.GetPlayerId(playerGuid);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryGetActiveHouseKey(
        GatewayConnection connection,
        out ulong houseId,
        out ulong ownerId,
        out int zoneDefinitionId)
    {
        houseId = 0;
        ownerId = 0;
        zoneDefinitionId = 0;

        if (connection.Player.Zone is not HousingZone housingZone ||
            housingZone.OwnerId is not ulong activeOwnerId ||
            !IsSupportedZoneDefinition(housingZone.DefinitionId))
        {
            return false;
        }

        houseId = housingZone.HouseId;
        ownerId = activeOwnerId;
        zoneDefinitionId = housingZone.DefinitionId;
        return true;
    }

    private static bool IsHousingSystem(string system)
    {
        return string.IsNullOrWhiteSpace(system) ||
            string.Equals(system, HousingSystem, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedZoneDefinition(int zoneDefinitionId)
    {
        return _resourceManager.Zones.TryGetValue(zoneDefinitionId, out var definition) &&
            definition is HousingZoneDefinition;
    }

    private static HousingZoneDefinition[] GetSupportedDefinitions()
    {
        return _resourceManager.Zones.Values
            .OfType<HousingZoneDefinition>()
            .OrderBy(definition => definition.Id)
            .ToArray();
    }

    private static void LogMalformed(
        GatewayConnection connection,
        byte subOpCode,
        ReadOnlySpan<byte> payload)
    {
        _logger.LogWarning(
            "Malformed rating packet {SubOpCode} from player {PlayerGuid}. Data={Data}",
            subOpCode,
            connection.Player.Guid,
            Convert.ToHexString(payload));
    }
}
