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
public static class RatingPacketRequestFeaturedHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(RatingPacketRequestFeaturedHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!RatingPacketRequestFeatured.TryDeserialize(data, out var ratingPacketRequestFeatured))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(RatingPacketRequestFeatured));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(RatingPacketRequestFeatured), ratingPacketRequestFeatured);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouses = HousingHelper.GetPublishedHouses(dbContext).ToList().OrderByDescending(HousingHelper.GetRating);

        var featuredRatingDataEntry = HousingHelper.GetRatingDataEntries(_resourceManager, dbHouses).FirstOrDefault();

        if (featuredRatingDataEntry is null)
            return true;

        connection.SendTunneled(new RatingPacketSendFeatured
        {
            Guid = connection.Player.Guid,
            System = HousingHelper.RatingSystem,
            Featured = featuredRatingDataEntry
        });

        return true;
    }
}