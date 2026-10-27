using System;

using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Game.Conversations;
using Sanctuary.Game.Quests;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

/// <summary>
/// The client asks to close its NPC dialog (Escape, or leaving it some other way) and stays locked in it until
/// the server confirms, so always confirm and drop whatever dialogue the player was in.
/// </summary>
[PacketHandler]
public static class CommandPacketEndDialogHandler
{
    private static INpcContentManager _npcContentManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _npcContentManager = serviceProvider.GetRequiredService<INpcContentManager>();
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        QuestDialogue.Clear(connection.Player);

        if (connection.Player.ActiveConversation is not null)
            _npcContentManager.EndConversation(connection.Player);
        else
            connection.Player.SendTunneled(new CommandPacketEndDialog());

        return true;
    }
}
