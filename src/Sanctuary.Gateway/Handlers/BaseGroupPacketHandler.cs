using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseGroupPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseGroupPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out short opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return opCode switch
        {
            GroupPacketGroupInvite.OpCode => GroupPacketGroupInviteHandler.HandlePacket(connection, reader.Span),
            GroupPacketGroupLeave.OpCode => GroupPacketGroupLeaveHandler.HandlePacket(connection, reader.Span),
            GroupPacketGroupAccept.OpCode => GroupPacketGroupAcceptHandler.HandlePacket(connection, reader.Span),
            GroupPacketGroupKick.OpCode => GroupPacketGroupKickHandler.HandlePacket(connection, reader.Span),
            GroupChangingJobPacket.OpCode => GroupChangingJobPacketHandler.HandlePacket(connection, reader.Span),
            GroupMapPingPacket.OpCode => GroupMapPingPacketHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}
