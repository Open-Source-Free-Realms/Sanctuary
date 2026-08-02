using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

// Opcode 47 (BaseUiPacket / "Task" family). Not fully reverse-engineered yet - this
// handler currently only exists to log SelectQuestPacket (sub 12) and
// SelectedQuestLockedPacket (sub 13) in detail, since SelectedQuestLockedPacket has been
// observed firing unprompted on every login and SelectQuestPacket may be what the client
// sends when a player tries to view/select a quest (e.g. clicking a quest giver or a quest
// journal entry). Always returns false so the normal [UNHANDLED C->S] logging still fires.
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
            case 13: // SelectedQuestLockedPacket
                Console.WriteLine($"[BaseUiPacket] SelectedQuestLockedPacket from {connection.Player.Name}. Remaining bytes: {Convert.ToHexString(reader.RemainingSpan)}");
                break;
            case 6: // SelectTaskRequest - fired by the objective-helper "SelectedTask(guid)" FR_event.
                _logger.LogInformation("[BaseUiPacket] SelectTaskRequest from {name}. Payload: {data}", connection.Player.Name, Convert.ToHexString(reader.RemainingSpan));
                break;
            default:
                Console.WriteLine($"[BaseUiPacket] sub-opcode {subOpCode} from {connection.Player.Name}. Remaining bytes: {Convert.ToHexString(reader.RemainingSpan)}");
                break;
        }

        return false;
    }
}
