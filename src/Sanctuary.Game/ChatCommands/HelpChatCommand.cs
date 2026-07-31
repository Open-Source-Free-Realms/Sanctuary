using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class HelpChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "help";

    public string Usage => "";

    public string Description => "Prints a list of available commands.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public HelpChatCommand(IChatCommandManager ChatCommandManager)
    {
        _chatCommandManager = ChatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        var summaries = _chatCommandManager.Commands
            .Where(command => invoker.ChatCommandRole >= command.RequiredRole)
            .OrderBy(command => command.KeyWord)
            .Select(command => command.KeyWord + ": " + command.Description)
            .ToArray();

        var fullHelpString = "";

        foreach (var summary in summaries)
        {
            fullHelpString += _chatCommandManager.Prefix + summary + "\n";
        }

        fullHelpString = fullHelpString.TrimEnd('\n');
        ChatHelper.SendSystemMessage(invoker, fullHelpString);

        return true;
    }
}