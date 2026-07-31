using Sanctuary.Game.Entities;

namespace Sanctuary.Game.ChatCommands;

public enum ChatCommandRole
{
    Player = 0,
    Mod = 1,
    Admin = 2
}

public interface IChatCommandView
{
    string KeyWord { get; }
    string Usage { get; }
    string Description { get; }
    ChatCommandRole RequiredRole { get; }
}

public interface IChatCommand : IChatCommandView
{
    /// <summary>
    /// Handles the chat command.
    /// </summary>
    /// <param name="invoker">The player who invoked the command.</param>
    /// <param name="args">The arguments passed to the command.</param>
    /// <returns>True if the command was well-formed; otherwise, false.</returns>
    bool Handle(Player invoker, string[] args);
}