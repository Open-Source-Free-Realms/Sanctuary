using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseFishingPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseFishingPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out short subOpCode))
        {
            _logger.LogError("Failed to read fishing sub-opcode. Data: {data}", Convert.ToHexString(reader.Span));
            return false;
        }

        // Pass the FULL payload (including the 138 opcode + sub-opcode header), matching the
        // convention used by BaseMiniGamePacketHandler: each fishing packet's TryDeserialize
        // re-reads and validates that header via BaseFishingPacket.TryRead. Passing RemainingSpan
        // (header stripped) makes every TryDeserialize fail its header check.
        var data = reader.Span;

        return subOpCode switch
        {
            2 => FishingPacketRegisterPlayerRequestHandler.HandlePacket(connection, data),
            5 => FishingPacketCastAnimRequestHandler.HandlePacket(connection, data),
            6 => FishingPacketCastRequestHandler.HandlePacket(connection, data),
            7 => FishingPacketReelInRequestHandler.HandlePacket(connection, data),
            15 => FishingPacketSpecialRequestHandler.HandlePacket(connection, data),
            _ => HandleUnhandled(subOpCode, data)
        };
    }

    private static bool HandleUnhandled(short subOpCode, ReadOnlySpan<byte> payload)
    {
        _logger.LogInformation("Unhandled fishing sub-opcode {subOpCode}. Payload: {payload}",
            subOpCode, Convert.ToHexString(payload));
        return true;
    }
}
