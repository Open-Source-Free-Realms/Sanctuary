using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class RatingPacketSearchRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(RatingPacketSearchRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!RatingPacketSearchRequest.TryDeserialize(data, out var ratingPacketSearchRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(RatingPacketSearchRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(RatingPacketSearchRequest), ratingPacketSearchRequest);

        var searchText = ratingPacketSearchRequest.SearchText.Trim();

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouses = HousingHelper.GetPublishedHouses(dbContext).AsNoTracking().ToList()
            .Where(dbHouse => dbHouse.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                || (dbHouse.Character.FullName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || dbHouse.Keywords.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Take(HousingHelper.MaxRatingEntryCount);

        connection.SendTunneled(new RatingPacketSearchReply
        {
            Guid = connection.Player.Guid,
            System = HousingHelper.RatingSystem,
            Results = HousingHelper.GetRatingDataEntries(_resourceManager, dbHouses)
        });

        return true;
    }
}