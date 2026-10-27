using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class RewardChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IRewardManager _rewardManager;
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "reward";
    public string Usage => "<tableKey> | list";
    public string Description => "Rolls and grants a reward from the given reward table, for testing.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public RewardChatCommand(IChatCommandManager chatCommandManager, IRewardManager rewardManager, IResourceManager resourceManager)
    {
        _chatCommandManager = chatCommandManager;
        _rewardManager = rewardManager;
        _resourceManager = resourceManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 1)
            return false;

        var tableKey = args[0];

        if (tableKey.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            var keys = _resourceManager.RewardTables.Keys.Order().ToList();

            ChatHelper.SendSystemMessage(invoker, keys.Count == 0
                ? "No reward tables are loaded."
                : $"Reward tables: {string.Join(", ", keys)}");
            return true;
        }

        if (!_rewardManager.TryRollReward(tableKey, out var drop) || drop is null)
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown reward table '{tableKey}'.");
            return true;
        }

        if (!_rewardManager.TryGrantReward(invoker, drop))
        {
            ChatHelper.SendSystemMessage(invoker, "Failed to grant the rolled reward.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Reward test grant", null, $"table={tableKey}");
        ChatHelper.SendSystemMessage(invoker, $"Rolled and granted a reward from '{tableKey}'.");

        return true;
    }
}
