using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

// The player clicked a response button on a CommandPacketShowDialog (currently only the mid-quest
// "You got it!" reply bubble, see QuestManager.CompleteGoal). Reply with CommandPacketEndDialog to tear
// down just the conversation dialog/camera focus - NOT CommandPacketQuestDialogComplete, which is
// specific to the quest start/end screen and additionally fires "QuestStartHandler:DismissEndScreen";
// sending that here tore down UI that was never open and left the client's HUD/movement locked.
// Without this handler PacketDialogResponse was silently dropped and the camera stayed locked on the NPC.
[PacketHandler]
public static class PacketDialogResponseHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketDialogResponseHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketDialogResponse.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketDialogResponse));
            return false;
        }

        connection.Player.SendTunneled(new CommandPacketEndDialog());
        return true;
    }
}
