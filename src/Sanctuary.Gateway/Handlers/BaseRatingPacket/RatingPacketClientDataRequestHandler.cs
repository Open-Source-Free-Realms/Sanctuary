using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class RatingPacketClientDataRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(RatingPacketClientDataRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!RatingPacketClientDataRequest.TryDeserialize(data, out var ratingPacketClientDataRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(RatingPacketClientDataRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(RatingPacketClientDataRequest), ratingPacketClientDataRequest);

        var player = connection.Player;

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouses = HousingHelper.GetPublishedHouses(dbContext).AsNoTracking().ToList();

        var friendIds = player.Friends.Select(friend => GuidHelper.GetPlayerId(friend.Guid)).ToHashSet();

        IEnumerable<DbHouse> listedHouses = ratingPacketClientDataRequest.ListType switch
        {
            RatingListType.HighestRated => dbHouses.OrderByDescending(HousingHelper.GetRating),
            RatingListType.Friends => dbHouses.Where(dbHouse => friendIds.Contains(dbHouse.CharacterId)),
            RatingListType.Newest => dbHouses.OrderByDescending(dbHouse => dbHouse.PublishedAt),
            _ => dbHouses
        };

        var ratingPacketDataReply = new RatingPacketDataReply
        {
            Guid = player.Guid,
            System = HousingHelper.RatingSystem,
            ListType = ratingPacketClientDataRequest.ListType
        };

        foreach (var ratingDataEntry in HousingHelper.GetRatingDataEntries(_resourceManager, listedHouses.Take(HousingHelper.MaxRatingEntryCount)))
            ratingPacketDataReply.Entries[JenkinsHelper.OneAtATimeHash(ratingDataEntry.HouseGuid ?? string.Empty)] = ratingDataEntry;

        connection.SendTunneled(ratingPacketDataReply);

        return true;
    }
}