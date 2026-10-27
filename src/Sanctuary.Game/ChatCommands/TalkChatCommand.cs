using System;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Sent by the chat window mod's dialog box when a player picks a conversation choice or closes it.
/// </summary>
public class TalkChatCommand : IChatCommand
{
    public string KeyWord => "talk";
    public string Usage => "<npcGuid> <nodeId> <choice> | end";
    public string Description => "Answers or closes an NPC conversation (sent by the dialog box).";
    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 1 && args[0].Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            invoker.Zone.NpcContent.EndConversation(invoker);
            return true;
        }

        if (args.Length != 3
            || !ulong.TryParse(args[0], out var npcGuid)
            || !int.TryParse(args[1], out var nodeId)
            || !int.TryParse(args[2], out var index))
            return false;

        invoker.Zone.NpcContent.Choose(invoker, npcGuid, nodeId, index);

        return true;
    }
}
