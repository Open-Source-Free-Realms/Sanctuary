using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.ChatCommands;

public class ActivitiesChatCommand : IChatCommand
{
    public string KeyWord => "activities";
    public string Usage => "";
    public string Description => "Re-sends the Games window's activity list (the daily wheel, ...).";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public bool Handle(Player invoker, string[] args)
    {
        BaseZone.SendActivities(invoker);
        ChatHelper.SendSystemMessage(invoker, "Activity list sent.");
        return true;
    }
}
