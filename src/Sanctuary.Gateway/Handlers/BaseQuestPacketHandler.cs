using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseQuestPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseQuestPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out int subOpCode))
        {
            _logger.LogError("Failed to read sub-opcode from BaseQuestPacket. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return subOpCode switch
        {
            QuestReplyPacket.OpCode => QuestReplyPacketHandler.HandlePacket(connection, reader.Span),
            QuestAbandonedPacket.OpCode => QuestAbandonedPacketHandler.HandlePacket(connection, reader.Span),
            QuestEndPacket.SubOpCode + 1 => HandleQuestEndReply(connection), // sub 14: QuestEndReplyPacket
            _ => HandleUnknownOpCode(subOpCode)
        };
    }

    // The player clicked "Complete" on the quest end screen (sent after we deliver QuestEndPacket).
    // That end screen hides the HUD until the server acknowledges; restore the HUD + camera with the
    // same command the quest-accept flow uses (sub-opcode 29 -> client FUN_00a99220 ->
    // QuestStartHandler:DismissEndScreen). The reward/completion state was already applied server-side
    // when the player interacted with the turn-in NPC, so this handler only finalizes the UI.
    private static bool HandleQuestEndReply(GatewayConnection connection)
    {
        // Player confirmed on the end screen - finalize the quest (grant reward + celebration, mark
        // complete, clear badges) now, then restore the HUD the end screen hid.
        var pending = connection.Player.PendingQuestEndAction;
        connection.Player.PendingQuestEndAction = null;
        pending?.Invoke();

        connection.Player.SendTunneled(new CommandPacketQuestDialogComplete());
        return true;
    }

    private static bool HandleUnknownOpCode(int subOpCode)
    {
        _logger.LogWarning("Unknown BaseQuestPacket sub-opcode: {subOpCode}", subOpCode);
        return false;
    }
}
