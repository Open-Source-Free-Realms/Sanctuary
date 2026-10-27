using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

public class CoinsChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public string KeyWord => "coins";
    public string Usage => "<amount> [player] | set <amount> [player]";
    public string Description => "Adds (or with a negative amount, removes) coins, or sets the exact coin count.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public CoinsChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
        _dbContextFactory = dbContextFactory;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        var set = args[0].Equals("set", StringComparison.OrdinalIgnoreCase);

        if (set)
            args = args[1..];

        if (args.Length == 0 || !int.TryParse(args[0], out var amount) || (set && amount < 0))
            return false;

        var target = invoker;

        if (args.Length > 1)
        {
            var targetName = string.Join(' ', args[1..]);

            if (!_zoneManager.TryGetPlayer(targetName, out target))
            {
                ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
                return true;
            }
        }

        var characterId = GuidHelper.GetPlayerId(target.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();
        var dbCharacter = dbContext.Characters.SingleOrDefault(character => character.Id == characterId);

        if (dbCharacter is null)
            return true;

        dbCharacter.Coins = set ? amount : (int)Math.Clamp((long)dbCharacter.Coins + amount, 0, int.MaxValue);

        if (dbContext.SaveChanges() <= 0 && dbCharacter.Coins != target.Coins)
        {
            ChatHelper.SendSystemMessage(invoker, "Failed to save the coin count.");
            return true;
        }

        target.Coins = dbCharacter.Coins;
        target.SendTunneled(new ClientUpdatePacketCoinCount { Coins = target.Coins });

        _chatCommandManager.LogAction(this, invoker, set ? "Set" : "Add", target.Name.FullName,
            $"Amount: {amount}, Total: {target.Coins}");
        ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName} now has {target.Coins} coins.");

        return true;
    }
}
