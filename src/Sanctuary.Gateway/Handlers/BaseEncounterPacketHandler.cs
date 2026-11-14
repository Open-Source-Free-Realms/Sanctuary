using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseEncounterPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseEncounterPacketHandler));
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
            EncounterParticipantRequestEntrancePacket.OpCode => EncounterParticipantRequestEntrancePacketHandler.HandlePacket(connection, reader.Span),
            EncounterParticipantRequestExitPacket.OpCode => EncounterParticipantRequestExitPacketHandler.HandlePacket(connection, reader.Span),
            EncounterParticipantCancelPendingEncounterPacket.OpCode => EncounterParticipantCancelPendingEncounterPacketHandler.HandlePacket(connection, reader.Span),
            _ => false
        };
    }
}
