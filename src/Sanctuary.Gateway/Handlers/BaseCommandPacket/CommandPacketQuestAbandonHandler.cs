using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Quests;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

// The journal "Drop Quest" (red X) button. The client sends CommandPacketQuestAbandon
// (BaseCommandPacket opcode 26, sub-opcode 23) with the quest id it wants to drop; the quest manager
// removes it and tells the client to clear the journal entry.
[PacketHandler]
public static class CommandPacketQuestAbandonHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketQuestAbandonHandler));

        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    // data is the full BaseCommandPacket span: short OpCode(26) + short SubOpCode(23) + int QuestId.
    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        reader.TryRead(out short _);      // opcode 26
        reader.TryRead(out short _);      // sub-opcode 23
        reader.TryRead(out int questId);  // quest id the client wants to drop

        _questManager.AbandonQuest(connection.Player, questId);
        return true;
    }
}
