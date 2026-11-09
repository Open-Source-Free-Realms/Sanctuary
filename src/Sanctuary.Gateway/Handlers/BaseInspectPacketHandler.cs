using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseInspectPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseInspectPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out byte subOpCode))
        {
            _logger.LogError("Failed to read sub opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return subOpCode switch
        {
            StopInspectPacket.OpCode => StopInspectPacketHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}
