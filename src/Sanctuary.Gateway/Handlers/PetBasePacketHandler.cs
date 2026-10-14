using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PetBasePacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PetBasePacketHandler));
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
            PetEquipPacket.OpCode => PetEquipPacketHandler.HandlePacket(connection, reader.Span),
            PetRequestNamePacket.OpCode => PetRequestNamePacketHandler.HandlePacket(connection, reader.Span),
            PetTrickFavoriteSetPacket.OpCode => PetTrickFavoriteSetPacketHandler.HandlePacket(connection, reader.Span),
            PetUtilityPacket.OpCode => PetUtilityPacketHandler.HandlePacket(connection, reader.Span),
            PetUtilityFeedPacket.OpCode => PetUtilityFeedPacketHandler.HandlePacket(connection, reader.Span),
            PetUtilityGroomPacket.OpCode => PetUtilityGroomPacketHandler.HandlePacket(connection, reader.Span),
            PetTactileNotify.OpCode => PetTactileNotifyHandler.HandlePacket(connection, reader.Span),
            PetPerformTrickPacket.OpCode => PetPerformTrickPacketHandler.HandlePacket(connection, reader.Span),
            PetTrickGestureResult.OpCode => PetTrickGestureResultHandler.HandlePacket(connection, reader.Span),
            PetInWaterPacket.OpCode => PetInWaterPacketHandler.HandlePacket(connection, reader.Span),
            PetUiModePacket.OpCode => PetUiModePacketHandler.HandlePacket(connection, reader.Span),
            PetSummonRecallPacket.OpCode => PetSummonRecallPacketHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}
