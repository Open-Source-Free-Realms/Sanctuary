using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseRatingPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseRatingPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out byte opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return opCode switch
        {
            RatingPacketClientDataRequest.OpCode => RatingPacketClientDataRequestHandler.HandlePacket(connection, reader.Span),
            RatingPacketAddCandidate.OpCode => RatingPacketAddCandidateHandler.HandlePacket(connection, reader.Span),
            RatingPacketRemoveCandidate.OpCode => RatingPacketRemoveCandidateHandler.HandlePacket(connection, reader.Span),
            RatingPacketAddVote.OpCode => RatingPacketAddVoteHandler.HandlePacket(connection, reader.Span),
            RatingPacketSearchRequest.OpCode => RatingPacketSearchRequestHandler.HandlePacket(connection, reader.Span),
            RatingPacketCandidateInfoRequest.OpCode => RatingPacketCandidateInfoRequestHandler.HandlePacket(connection, reader.Span),
            RatingPacketRequestFeatured.OpCode => RatingPacketRequestFeaturedHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}