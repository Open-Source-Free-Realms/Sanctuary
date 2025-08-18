using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseCommandPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseCommandPacketHandler));
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
            CommandPacketInteractRequest.OpCode => CommandPacketInteractRequestHandler.HandlePacket(connection, reader.Span),
            CommandPacketInteractionSelect.OpCode => CommandPacketInteractionSelectHandler.HandlePacket(connection, reader.Span),
            CommandPacketSetProfile.OpCode => CommandPacketSetProfileHandler.HandlePacket(connection, reader.Span),
            CommandPacketSetChatBubbleColor.OpCode => CommandPacketSetChatBubbleColorHandler.HandlePacket(connection, reader.Span),
            CommandPacketSelectPlayer.OpCode => CommandPacketSelectPlayerHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}