using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseGuildPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseGuildPacketHandler));
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
            GuildCreatePacket.OpCode => GuildCreatePacketHandler.HandlePacket(connection, reader.Span),
            GuildInvitePacket.OpCode => GuildInvitePacketHandler.HandlePacket(connection, reader.Span),
            GuildPromotePacket.OpCode => GuildPromotePacketHandler.HandlePacket(connection, reader.Span),
            GuildDemotePacket.OpCode => GuildDemotePacketHandler.HandlePacket(connection, reader.Span),
            GuildRemovePacket.OpCode => GuildRemovePacketHandler.HandlePacket(connection, reader.Span),
            GuildQuitPacket.OpCode => GuildQuitPacketHandler.HandlePacket(connection, reader.Span),
            GuildInviteAcceptPacket.OpCode => GuildInviteAcceptPacketHandler.HandlePacket(connection, reader.Span),
            GuildInviteDeclinePacket.OpCode => GuildInviteDeclinePacketHandler.HandlePacket(connection, reader.Span),
            GuildInviteTimeOutPacket.OpCode => GuildInviteTimeOutPacketHandler.HandlePacket(connection, reader.Span),
            GuildNameRequestPacket.OpCode => GuildNameRequestPacketHandler.HandlePacket(connection, reader.Span),
            GuildPaidRenameCheckRequestPacket.OpCode => GuildPaidRenameCheckRequestPacketHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}