using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class RewardChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IResourceManager _resourceManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly ILogger _logger;

    public string KeyWord => "reward";
    public string Usage => "<tableKey>";
    public string Description => "Rolls and grants a reward from the given reward table, for testing.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Player;

    public RewardChatCommand(IChatCommandManager chatCommandManager, IResourceManager resourceManager,
        IDbContextFactory<DatabaseContext> dbContextFactory, ILogger<RewardChatCommand> logger)
    {
        _chatCommandManager = chatCommandManager;
        _resourceManager = resourceManager;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 1)
            return false;

        var tableKey = args[0];

        if (!_resourceManager.RewardTables.TryRoll(tableKey, out var drop))
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown reward table '{tableKey}'.");
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        if (!RewardHelper.TryGrantReward(_resourceManager, dbContext, _logger, invoker, drop))
        {
            ChatHelper.SendSystemMessage(invoker, "Failed to grant the rolled reward.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Reward test grant", null, $"table={tableKey}");
        ChatHelper.SendSystemMessage(invoker, $"Rolled and granted a reward from '{tableKey}'.");

        return true;
    }
}
