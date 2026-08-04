using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

// Opcode 47 (BaseUiPacket / "Task" family) - only sub-opcodes 12 and 13 have real packet classes; the rest are just logged.
[PacketHandler]
public static class BaseUiPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseUiPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        var fullBuffer = reader.Span;

        if (!reader.TryRead(out byte subOpCode))
        {
            _logger.LogError("Failed to read sub-opcode from BaseUiPacket. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        switch (subOpCode)
        {
            case SelectQuestPacket.OpCode:
                return SelectQuestPacketHandler.HandlePacket(connection, fullBuffer);
            case SelectedQuestLockedPacket.OpCode:
                return SelectedQuestLockedPacketHandler.HandlePacket(connection, fullBuffer);
            case 6: // SelectTaskRequest - fired by the objective-helper "SelectedTask(guid)" FR_event.
                _logger.LogInformation("SelectTaskRequest from {name}. Payload: {data}", connection.Player.Name, Convert.ToHexString(reader.RemainingSpan));
                return false;
            default:
                _logger.LogInformation("Sub-opcode {sub} from {name}. Payload: {data}", subOpCode, connection.Player.Name, Convert.ToHexString(reader.RemainingSpan));
                return false;
        }
    }
}
