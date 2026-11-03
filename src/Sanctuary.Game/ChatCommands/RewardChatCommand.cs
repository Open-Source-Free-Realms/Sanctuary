using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Uses rewards defined in Rewards.json
/// </summary>
public class RewardChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IRewardManager _rewardManager;
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "reward";
    public string Usage => "[\"First Last\"] <tableKey>";
    public string Description => "Rolls and grants a reward from the given reward table either to you or to an online player, if specified.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public RewardChatCommand(IChatCommandManager chatCommandManager, IRewardManager rewardManager, IZoneManager zoneManager)
    {
        _chatCommandManager = chatCommandManager;
        _rewardManager = rewardManager;
        _zoneManager = zoneManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        var tableKey = args[^1];
        var target = invoker;

        if (args.Length > 1)
        {
            var targetName = string.Join(' ', args[..^1]).Trim('"');

            if (!_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
            {
                ChatHelper.SendSystemMessage(invoker, $"Invalid name specified. No online player named \"{targetName}\".");
                return true;
            }
            target = targetPlayer;
        }

        if (!_rewardManager.TryRollRewardUnowned(target, tableKey, out var drop, out var tint) || drop is null)
        {
            ChatHelper.SendSystemMessage(invoker, $"Nothing left to win in '{tableKey}'.");
            return true;
        }

        var granted = drop is ItemRewardDropDefinition item
            ? _rewardManager.TryGrantItem(target, item.ItemDefinitionId, tint, item.Quantity)
            : _rewardManager.TryGrantReward(target, drop);

        if (!granted)
        {
            ChatHelper.SendSystemMessage(invoker, "Failed to grant the rolled reward.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Reward grant", target.Name.FullName, $"table={tableKey}");
        ChatHelper.SendSystemMessage(invoker, $"Rolled and granted a reward from '{tableKey}' to {target.Name.FullName}.");

        return true;
    }
}
