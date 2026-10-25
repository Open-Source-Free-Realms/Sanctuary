using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Quests;

namespace Sanctuary.Game.ChatCommands;

public class QuestChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IQuestManager _questManager;
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "quest";

    public string Usage => "reset <questId> [player]";

    public string Description => "Removes a quest from a player's log so it can be taken again.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public QuestChatCommand(IChatCommandManager chatCommandManager, IQuestManager questManager, IZoneManager zoneManager)
    {
        _chatCommandManager = chatCommandManager;
        _questManager = questManager;
        _zoneManager = zoneManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length < 2 || args[0].ToLowerInvariant() != "reset" || !int.TryParse(args[1], out var questId))
            return false;

        var targetName = args.Length > 2 ? string.Join(' ', args[2..]) : null;
        var target = invoker;

        if (targetName is not null && !_zoneManager.TryGetPlayer(targetName, out target))
        {
            ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
            return true;
        }

        if (!_questManager.ResetQuest(target, questId))
        {
            ChatHelper.SendSystemMessage(invoker, $"Quest {questId} is not in that player's log.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Reset quest", targetName, questId.ToString());
        ChatHelper.SendSystemMessage(invoker, $"Reset quest {questId}.");
        return true;
    }
}
