using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class RatingPacketAddVoteHandler
{
    private const float MinRating = 1.0f;
    private const float MaxRating = 5f;

    private const int MaxCategoryLength = 32;

    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(RatingPacketAddVoteHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!RatingPacketAddVote.TryDeserialize(data, out var ratingPacketAddVote))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(RatingPacketAddVote));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(RatingPacketAddVote), ratingPacketAddVote);

        var player = connection.Player;

        using var dbContext = _dbContextFactory.CreateDbContext();

        if (!HousingHelper.TryGetHouse(_resourceManager, dbContext, ratingPacketAddVote.HouseGuid, out var houseGuid, out var dbHouse)
            || !HousingHelper.CanVote(dbHouse, player.Guid)
            || ratingPacketAddVote.Rating is < MinRating or > MaxRating)
        {
            _logger.LogWarning("{player} could not vote on house {houseGuid}.", player.Guid, ratingPacketAddVote.HouseGuid);

            connection.SendTunneled(new RatingPacketVoteReply
            {
                Guid = player.Guid,
                Success = false
            });

            return true;
        }

        dbHouse.Votes.Add(new DbHouseVote
        {
            VoterId = GuidHelper.GetPlayerId(player.Guid),
            Rating = ratingPacketAddVote.Rating,
            Category = ratingPacketAddVote.Category[..Math.Min(ratingPacketAddVote.Category.Length, MaxCategoryLength)]
        });

        dbContext.SaveChanges();

        connection.SendTunneled(new RatingPacketVoteReply
        {
            Guid = player.Guid,
            Success = true
        });

        HousingHelper.SendCandidateInfo(player, houseGuid, dbHouse);

        return true;
    }
}