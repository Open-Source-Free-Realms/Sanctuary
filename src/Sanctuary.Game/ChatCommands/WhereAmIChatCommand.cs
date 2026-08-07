using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class WhereAmIChatCommand : IChatCommand
{
    public string KeyWord => "whereami";
    public string Usage => "";
    public string Description => "Prints your current position.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public bool Handle(Player invoker, string[] args)
    {
        var position = invoker.Position;
        ChatHelper.SendSystemMessage(invoker,
            $"Position: ({position.X:F2}, {position.Y:F2}, {position.Z:F2})");
        return true;
    }
}