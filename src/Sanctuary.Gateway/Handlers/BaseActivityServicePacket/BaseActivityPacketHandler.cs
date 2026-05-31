using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseActivityPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseActivityPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader, int serverType)
    {
        if (!reader.TryRead(out byte opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return opCode switch
        {
            ActivityPacketJoinActivityRequest.OpCode => ActivityPacketJoinActivityRequestHandler.HandlePacket(connection, reader.Span, serverType),
            ActivityPacketActivityListRefreshRequest.OpCode => ActivityPacketActivityListRefreshRequestHandler.HandlePacket(connection, reader.Span, serverType),
            _ => false
        };
    }
}