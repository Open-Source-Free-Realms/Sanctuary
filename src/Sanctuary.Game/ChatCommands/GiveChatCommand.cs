using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class GiveChatCommand : IChatCommand
{
    private const int MaxQuantity = 999;

    private readonly IChatCommandManager _chatCommandManager;
    private readonly IRewardManager _rewardManager;
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "give";
    public string Usage => "<item_id> [quantity] [tint_id]";
    public string Description => "Referee only. Puts items in your inventory.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public GiveChatCommand(IChatCommandManager chatCommandManager, IRewardManager rewardManager, IResourceManager resourceManager)
    {
        _chatCommandManager = chatCommandManager;
        _rewardManager = rewardManager;
        _resourceManager = resourceManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length is < 1 or > 3 || !int.TryParse(args[0], out var itemDefinitionId))
            return false;

        var quantity = 1;
        var tint = 0;

        if (args.Length >= 2 && (!int.TryParse(args[1], out quantity) || quantity <= 0 || quantity > MaxQuantity))
            return false;

        if (args.Length == 3 && !int.TryParse(args[2], out tint))
            return false;

        if (!_resourceManager.ClientItemDefinitions.TryGetValue(itemDefinitionId, out var itemDefinition))
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown item {itemDefinitionId}.");
            return true;
        }

        if (!_rewardManager.TryGrantItem(invoker, itemDefinitionId, tint, quantity))
        {
            ChatHelper.SendSystemMessage(invoker, $"Couldn't give item {itemDefinitionId}.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Give Item", $"#{itemDefinitionId}", $"quantity={quantity}, tint={tint}");
        ChatHelper.SendSystemMessage(invoker, $"Gave {quantity}x {itemDefinition.Comment ?? $"item {itemDefinitionId}"}.");
        return true;
    }
}
