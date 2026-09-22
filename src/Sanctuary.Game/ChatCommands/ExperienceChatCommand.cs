using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class ExperienceChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly ILogger _logger;

    public string KeyWord => "exp";
    public string Usage => "<profileId> <amount>";
    public string Description => "Grants experience (stars) to the given job profile, for testing.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public ExperienceChatCommand(IChatCommandManager chatCommandManager, IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory, ILogger<ExperienceChatCommand> logger)
    {
        _chatCommandManager = chatCommandManager;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 2 || !int.TryParse(args[0], out var profileId) || !int.TryParse(args[1], out var amount))
            return false;

        using var dbContext = _dbContextFactory.CreateDbContext();

        if (!RewardHelper.TryGrantExperience(_resourceManager, dbContext, _logger, invoker, profileId, amount))
        {
            ChatHelper.SendSystemMessage(invoker, $"Failed to grant experience for profile {profileId}.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Experience test grant", null, $"profileId={profileId}, amount={amount}");
        ChatHelper.SendSystemMessage(invoker, $"Granted {amount} experience to profile {profileId}.");

        return true;
    }
}
