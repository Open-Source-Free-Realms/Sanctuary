using System;
using System.Globalization;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class RatingPacketAddCandidateHandler
{
    private const int MaxDescriptionLength = 256;
    private const int MaxKeywordsLength = 128;

    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(RatingPacketAddCandidateHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!RatingPacketAddCandidate.TryDeserialize(data, out var ratingPacketAddCandidate))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(RatingPacketAddCandidate));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(RatingPacketAddCandidate), ratingPacketAddCandidate);

        var player = connection.Player;

        if (ratingPacketAddCandidate.OwnerGuid != player.Guid
            || !ulong.TryParse(ratingPacketAddCandidate.InstanceGuid, CultureInfo.InvariantCulture, out var houseGuid)
            || !HousingHelper.TryGetOwnedHouse(_resourceManager, _dbContextFactory, player, houseGuid, out var ownedHouse)
            || HousingHelper.IsStaffOnlyHouse(_resourceManager, ownedHouse.Definition))
        {
            _logger.LogWarning("{player} could not publish house {houseGuid}.", player.Guid, ratingPacketAddCandidate.InstanceGuid);
            return true;
        }

        var houseId = GuidHelper.GetHouseId(houseGuid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbHouse = dbContext.Houses.Include(house => house.Votes).First(house => house.Id == houseId);

        dbHouse.IsPublished = true;
        dbHouse.PublishedAt = DateTimeOffset.UtcNow;
        dbHouse.Description = ratingPacketAddCandidate.Description[..Math.Min(ratingPacketAddCandidate.Description.Length, MaxDescriptionLength)];
        dbHouse.Keywords = ratingPacketAddCandidate.Keywords[..Math.Min(ratingPacketAddCandidate.Keywords.Length, MaxKeywordsLength)];

        dbContext.SaveChanges();

        HousingHelper.SendCandidateInfo(player, houseGuid, dbHouse);

        return true;
    }
}