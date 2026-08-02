using System.Collections.ObjectModel;

using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;

namespace Sanctuary.Game;

public interface IChatCommandManager
{
    string Prefix { get; }

    ReadOnlyCollection<IChatCommandView> Commands { get; }

    bool Load();

    /// <summary>
    /// Attempts to handle a chat command.
    /// </summary>
    /// <param name="invoker">The player invoking the command.</param>
    /// <param name="command">The command string.</param>
    /// <returns>True if the command was handled; otherwise, false.</returns>
    bool TryHandle(Player invoker, string command);

    void LogAction(IChatCommand command, Player invoker, string action, string? targetName, string? detail);
}