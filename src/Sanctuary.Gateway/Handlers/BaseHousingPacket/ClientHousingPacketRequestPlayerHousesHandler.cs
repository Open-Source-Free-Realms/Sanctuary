using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ClientHousingPacketRequestPlayerHousesHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ClientHousingPacketRequestPlayerHousesHandler));
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!ClientHousingPacketRequestPlayerHouses.TryDeserialize(data, out _))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ClientHousingPacketRequestPlayerHouses));
            return false;
        }

        _logger.LogTrace("Received {name} packet.", nameof(ClientHousingPacketRequestPlayerHouses));

        SendHouseList(connection);
        return true;
    }

    public static void SendHouseList(GatewayConnection connection)
    {
        var characterId = GuidHelper.GetPlayerId(connection.Player.Guid);
        var supportedZoneDefinitionIds = _resourceManager.Zones.Values
            .OfType<HousingZoneDefinition>()
            .Select(definition => definition.Id)
            .ToArray();

        using var dbContext = _dbContextFactory.CreateDbContext();
        var houses = dbContext.Houses
            .AsNoTracking()
            .Where(house =>
                house.CharacterId == characterId &&
                supportedZoneDefinitionIds.Contains(house.ZoneDefinitionId))
            .OrderBy(house => house.Id)
            .Select(house => new
            {
                House = house,
                FixtureCount = house.Fixtures.Count
            })
            .ToList();
        var packet = new HousingPacketInstanceList
        {
            PlayerGuid = connection.Player.Guid
        };

        foreach (var entry in houses)
        {
            var house = entry.House;
            if (!_resourceManager.Zones.TryGetValue(house.ZoneDefinitionId, out var definition) ||
                definition is not HousingZoneDefinition housingDefinition)
            {
                continue;
            }

            packet.Instances.Add(new PlayerHousingInstanceInfo
            {
                OwnerGuid = connection.Player.Guid,
                InstanceGuid = GuidHelper.GetHouseGuid(house.Id),
                NameId = housingDefinition.NameId,
                OwnerName = connection.Player.Name.FullName,
                HouseName = string.IsNullOrWhiteSpace(house.Name) ? housingDefinition.DisplayName : house.Name,
                IconId = housingDefinition.IconId,
                FixtureCount = entry.FixtureCount,
                FurnitureScore = house.FurnitureScore,
                LastVisited = house.LastVisited,
                IsLocked = house.IsLocked,
                IsMembersOnly = house.IsMembersOnly,
                IsFloraAllowed = house.IsFloraAllowed,
                Description = house.Description,
                KeywordList = house.KeywordList,
                Unknown21 = string.Empty,
                Rating = house.Rating,
                Votes = house.Votes,
                HasRating = house.IsPublished,
                CanVote = false,
                WhenCreated = house.Created
            });
        }

        connection.SendTunneled(packet);
    }
}
