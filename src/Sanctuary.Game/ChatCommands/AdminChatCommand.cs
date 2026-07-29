using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class AdminChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "admin";

    public string Usage => "promote|demote <player>";

    public string Description => "Admin command for promoting or demoting players to/from Moderator.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public AdminChatCommand(IChatCommandManager chatCommandManager, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager)
    {
        _chatCommandManager = chatCommandManager;
        _dbContextFactory = dbContextFactory;
        _zoneManager = zoneManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length < 2)
            return false;

        var subCommand = args[0].ToLower();
        var target = string.Join(' ', args[1..]);

        switch (subCommand)
        {
            case "promote":
                Promote(invoker, target);
                break;
            case "demote":
                Demote(invoker, target);
                break;
            default:
                return false;
        }
        
        return true;
    }

    private void Promote(Player invoker, string target)
    {
        SetMod(invoker, target, true);
    }

    private void Demote(Player invoker, string target)
    {
        SetMod(invoker, target, false);
    }

    private void SetMod(Player invoker, string targetName, bool isMod)
    {
        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        var target = dbContext.Characters.SingleOrDefault(character => character.FullName == targetName);

        if (target is null)
        {
            ChatHelper.SendSystemMessage(invoker, $"No player named \"{targetName}\" was found.");
            return;
        }

        dbContext.Users
            .Where(user => user.Id == target.UserId)
            .ExecuteUpdate(user => user.SetProperty(u => u.IsMod, isMod));

        if (_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
            targetPlayer.IsMod = isMod;

        _chatCommandManager.LogAction(this, invoker, isMod ? "Promote" : "Demote", targetName, null);

        ChatHelper.SendSystemMessage(invoker, isMod
            ? $"{targetName} has been promoted to moderator."
            : $"{targetName} has been demoted from moderator.");
    }
}