using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class RatingPacketRemoveCandidateHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(RatingPacketRemoveCandidateHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!RatingPacketRemoveCandidate.TryDeserialize(data, out var ratingPacketRemoveCandidate))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(RatingPacketRemoveCandidate));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(RatingPacketRemoveCandidate), ratingPacketRemoveCandidate);

        var player = connection.Player;

        using var dbContext = _dbContextFactory.CreateDbContext();

        if (!HousingHelper.TryGetHouse(dbContext, ratingPacketRemoveCandidate.InstanceGuid, out var houseGuid, out var dbHouse)
            || GuidHelper.GetPlayerGuid(dbHouse.CharacterId) != player.Guid)
        {
            _logger.LogWarning("{player} could not remove house {houseGuid} from the directory.", player.Guid, ratingPacketRemoveCandidate.InstanceGuid);
            return true;
        }

        dbHouse.IsPublished = false;
        dbHouse.PublishedAt = null;
        dbHouse.Votes.Clear();

        dbContext.SaveChanges();

        HousingHelper.SendCandidateInfo(player, houseGuid, dbHouse);

        return true;
    }
}