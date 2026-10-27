using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

public class TitleChatCommand : IChatCommand
{
    private const int PageSize = 15;

    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public string KeyWord => "title";
    public string Usage => "list [page] | grant <id> [player] | revoke <id> [player]";
    public string Description => "Lists, grants, or revokes player titles. A granted title is equipped at once; it shows in the title list after relog.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public TitleChatCommand(IZoneManager zoneManager, IResourceManager resourceManager, IChatCommandManager chatCommandManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
        _dbContextFactory = dbContextFactory;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "list":
                return HandleList(invoker, args[1..]);

            case "grant":
            case "revoke":
                break;

            default:
                return false;
        }

        var grant = args[0].Equals("grant", StringComparison.OrdinalIgnoreCase);

        if (args.Length < 2 || !int.TryParse(args[1], out var titleId))
            return false;

        if (!_resourceManager.PlayerTitles.TryGetValue(titleId, out var title))
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown title {titleId}. Use !{KeyWord} list.");
            return true;
        }

        var target = invoker;

        if (args.Length > 2)
        {
            var targetName = string.Join(' ', args[2..]);

            if (!_zoneManager.TryGetPlayer(targetName, out target))
            {
                ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
                return true;
            }
        }

        var characterId = GuidHelper.GetPlayerId(target.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var hasTitle = dbContext.Titles.Any(x => x.CharacterId == characterId && x.Id == titleId);

        if (grant)
        {
            if (!hasTitle)
            {
                dbContext.Titles.Add(new DbTitle { Id = titleId, CharacterId = characterId });
                dbContext.SaveChanges();
            }

            if (!target.Titles.Any(x => x.Id == titleId))
                target.Titles.Add(title);

            SetActiveTitle(target, title.Id);
        }
        else
        {
            if (!hasTitle)
            {
                ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName} does not have title {titleId}.");
                return true;
            }

            dbContext.Titles.Where(x => x.CharacterId == characterId && x.Id == titleId).ExecuteDelete();

            target.Titles.RemoveAll(x => x.Id == titleId);

            if (target.ActiveTitle == titleId)
                SetActiveTitle(target, 0);
        }

        _chatCommandManager.LogAction(this, invoker, grant ? "Grant" : "Revoke", target.Name.FullName, $"Title: {titleId}");
        ChatHelper.SendSystemMessage(invoker, $"{(grant ? "Granted" : "Revoked")} title {titleId} {(grant ? "to" : "from")} {target.Name.FullName}.");

        return true;
    }

    private bool HandleList(Player invoker, string[] args)
    {
        var page = 1;

        if (args.Length > 0 && (!int.TryParse(args[0], out page) || page < 1))
            return false;

        var titles = _resourceManager.PlayerTitles.Values.OrderBy(x => x.Id).ToList();
        var pageCount = Math.Max(1, (titles.Count + PageSize - 1) / PageSize);

        ChatHelper.SendSystemMessage(invoker, $"Titles (page {Math.Min(page, pageCount)}/{pageCount}):");

        foreach (var title in titles.Skip((page - 1) * PageSize).Take(PageSize))
        {
            var owned = invoker.Titles.Any(x => x.Id == title.Id) ? " (owned)" : string.Empty;
            ChatHelper.SendSystemMessage(invoker, $"{title.Id,-6} NameId {title.NameId}, Type {title.Type}{owned}");
        }

        return true;
    }

    private static void SetActiveTitle(Player player, int titleId)
    {
        player.ActiveTitle = titleId;

        var playerUpdatePacketPlayerTitle = new PlayerUpdatePacketPlayerTitle
        {
            Guid = player.Guid
        };

        var title = player.Titles.FirstOrDefault(x => x.Id == titleId);

        if (title is not null)
            playerUpdatePacketPlayerTitle.Title = title;

        player.SendTunneledToVisible(playerUpdatePacketPlayerTitle, true);
    }
}
