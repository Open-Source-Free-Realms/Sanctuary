using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketEndDialogHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketEndDialogHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketEndDialog.TryDeserialize(data, out _))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CommandPacketEndDialog));
            return false;
        }

        if (connection.Player.QuestConversation.Current is { Closable: true })
            QuestDialogue.Clear(connection.Player);

        return true;
    }
}
