using System;
using System.Linq;

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
public static class RatingPacketCandidateInfoRequestHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(RatingPacketCandidateInfoRequestHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!RatingPacketCandidateInfoRequest.TryDeserialize(data, out var ratingPacketCandidateInfoRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(RatingPacketCandidateInfoRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(RatingPacketCandidateInfoRequest), ratingPacketCandidateInfoRequest);

        var player = connection.Player;

        var ratingPacketCandidateInfoReply = new RatingPacketCandidateInfoReply
        {
            Guid = player.Guid
        };

        using var dbContext = _dbContextFactory.CreateDbContext();

        if (string.IsNullOrEmpty(ratingPacketCandidateInfoRequest.InstanceGuid))
        {
            var ownerId = GuidHelper.GetPlayerId(ratingPacketCandidateInfoRequest.OwnerGuid);

            foreach (var dbHouse in HousingHelper.GetPublishedHouses(dbContext).AsNoTracking().Where(house => house.CharacterId == ownerId))
            {
                ratingPacketCandidateInfoReply.Candidates.Add(HousingHelper.GetCandidateRatingInfo(HousingHelper.GetHouseGuid(dbHouse), dbHouse, player.Guid));
            }
        }
        else if (HousingHelper.TryGetHouse(dbContext, ratingPacketCandidateInfoRequest.InstanceGuid, out var houseGuid, out var dbHouse)
            && dbHouse.IsPublished)
        {
            ratingPacketCandidateInfoReply.Candidates.Add(HousingHelper.GetCandidateRatingInfo(houseGuid, dbHouse, player.Guid));
        }

        connection.SendTunneled(ratingPacketCandidateInfoReply);

        return true;
    }
}