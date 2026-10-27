using Sanctuary.Game.Authoring;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Redeems a code made with !code. The client's own "Enter a game code" box goes to SOE's commerce
/// web service instead, which can't be pointed at this server.
/// </summary>
public class RedeemChatCommand : IChatCommand
{
    private readonly IRedeemCodeManager _redeemCodeManager;

    public string KeyWord => "redeem";
    public string Usage => "[code]";
    public string Description => "Redeems a code for items or coins.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public RedeemChatCommand(IRedeemCodeManager redeemCodeManager)
    {
        _redeemCodeManager = redeemCodeManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        // A bare !redeem (the dock's Redeem Code button) opens the chat window mod's code popup.
        if (args.Length == 0)
        {
            ChatHelper.SendGmPanelData(invoker, "redeem", "open");
            return true;
        }

        if (args.Length != 1)
            return false;

        ChatHelper.SendSystemMessage(invoker, _redeemCodeManager.Redeem(invoker, args[0]));

        return true;
    }
}
