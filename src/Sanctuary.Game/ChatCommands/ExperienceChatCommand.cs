using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class ExperienceChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IRewardManager _rewardManager;

    public string KeyWord => "exp";
    public string Usage => "<profileId> <amount>";
    public string Description => "Grants experience (stars) to the given job profile, for testing.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public ExperienceChatCommand(IChatCommandManager chatCommandManager, IRewardManager rewardManager)
    {
        _chatCommandManager = chatCommandManager;
        _rewardManager = rewardManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 2 || !int.TryParse(args[0], out var profileId) || !int.TryParse(args[1], out var amount))
            return false;

        if (!_rewardManager.TryGrantExperience(invoker, profileId, amount))
        {
            ChatHelper.SendSystemMessage(invoker, $"Failed to grant experience for profile {profileId}.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Experience test grant", null, $"profileId={profileId}, amount={amount}");
        ChatHelper.SendSystemMessage(invoker, $"Granted {amount} experience to profile {profileId}.");

        return true;
    }
}
