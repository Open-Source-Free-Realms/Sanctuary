using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketBaseInGamePurchaseHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketBaseInGamePurchaseHandler));
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
            InGamePurchaseUpdateItemRequirementsRequest.OpCode => InGamePurchaseUpdateItemRequirementsRequestHandler.HandlePacket(connection),
            _ => LogUnhandled(opCode, reader)
        };
    }

    private static bool LogUnhandled(short opCode, PacketReader reader)
    {
        // Client statistics, server status, merchant and Station Cash product requests are informational or unused here.
        _logger.LogDebug("Unhandled in-game purchase packet {opCode}. ( Data: {data} )", opCode, Convert.ToHexString(reader.Span));

        return true;
    }
}