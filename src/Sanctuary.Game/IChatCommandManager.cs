using System.Collections.ObjectModel;

using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;

namespace Sanctuary.Game;

public interface IChatCommandManager
{
    string Prefix { get; }

    ReadOnlyCollection<IChatCommandView> Commands { get; }

    bool Load();

    bool TryHandle(Player invoker, string command);

    void LogAction(IChatCommand command, Player invoker, string action, string? targetName, string? detail);
}