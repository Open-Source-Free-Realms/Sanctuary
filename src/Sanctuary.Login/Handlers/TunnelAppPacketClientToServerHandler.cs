using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Packet;
using Sanctuary.Core.IO;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class TunnelAppPacketClientToServerHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(LoginRequestHandler));
    }

    public static bool HandlePacket(LoginConnection connection, Span<byte> data)
    {
        if (!TunnelAppPacketClientToServer.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(TunnelAppPacketClientToServer));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(TunnelAppPacketClientToServer), packet);

        var reader = new PacketReader(packet.Payload);

        if (!reader.TryRead(out short opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(data));
            return false;
        }

        return opCode switch
        {
            PacketCheckNameRequest.OpCode => PacketCheckNameRequestHandler.HandlePacket(connection, packet.Payload),
            _ => false
        };
    }
}