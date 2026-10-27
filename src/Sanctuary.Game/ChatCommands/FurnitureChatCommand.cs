using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class FurnitureChatCommand : IChatCommand
{
    private const int DefaultQuantity = 999;

    private readonly IChatCommandManager _chatCommandManager;
    private readonly IHousingManager _housingManager;

    public string KeyWord => "furniture";
    public string Usage => "[quantity]";
    public string Description => "Referee only. Gives you every placeable housing item (999 of each by default).";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public FurnitureChatCommand(IChatCommandManager chatCommandManager, IHousingManager housingManager)
    {
        _chatCommandManager = chatCommandManager;
        _housingManager = housingManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        var quantity = DefaultQuantity;

        if (args.Length > 1 || (args.Length == 1 && (!int.TryParse(args[0], out quantity) || quantity <= 0 || quantity > DefaultQuantity)))
            return false;

        var granted = _housingManager.GrantAllFurniture(invoker, quantity);

        _chatCommandManager.LogAction(this, invoker, "Grant All Furniture", null, $"quantity={quantity}, stacks={granted}");

        ChatHelper.SendSystemMessage(invoker, granted > 0
            ? $"Granted {granted} housing item stack(s) at {quantity} each."
            : "You already have every housing item.");

        return true;
    }
}
