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

/// <summary>
/// A command that also needs the text after its keyword exactly as typed, e.g. to keep runs of spaces
/// in script source (see <see cref="ChatCommandText.Remainder"/>).
/// </summary>
public interface IRawChatCommand : IChatCommand
{
    /// <param name="rawArgs">Everything after the keyword and the single space that follows it.</param>
    bool Handle(Player invoker, string[] args, string rawArgs);
}