using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class ProfileChatCommand : IChatCommand
{
    private const int PageSize = 15;
    private const int MaxLevel = 20;

    private readonly ILogger _logger;
    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public string KeyWord => "profile";
    public string Usage => "list [page] | grant <id> [level] [player] | level <id> <level> [player]";
    public string Description => "Lists profiles (jobs), grants one with its default items, or sets a profile's level. Changes show after relog.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public ProfileChatCommand(ILogger<ProfileChatCommand> logger, IZoneManager zoneManager, IResourceManager resourceManager, IChatCommandManager chatCommandManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
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
                return HandleGrant(invoker, args[1..]);

            case "level":
                return HandleLevel(invoker, args[1..]);

            default:
                return false;
        }
    }

    private bool HandleList(Player invoker, string[] args)
    {
        var page = 1;

        if (args.Length > 0 && (!int.TryParse(args[0], out page) || page < 1))
            return false;

        var profiles = _resourceManager.Profiles.Values.OrderBy(x => x.Id).ToList();
        var pageCount = Math.Max(1, (profiles.Count + PageSize - 1) / PageSize);

        ChatHelper.SendSystemMessage(invoker, $"Profiles (page {Math.Min(page, pageCount)}/{pageCount}):");

        foreach (var profile in profiles.Skip((page - 1) * PageSize).Take(PageSize))
        {
            var owned = invoker.Profiles.FirstOrDefault(x => x.Id == profile.Id);
            var ownedText = owned is null ? string.Empty : $" (owned, level {owned.Rank})";

            ChatHelper.SendSystemMessage(invoker, $"{profile.Id,-6} NameId {profile.NameId}, Type {profile.Type}{ownedText}");
        }

        return true;
    }

    private bool HandleGrant(Player invoker, string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var profileId))
            return false;

        var level = 1;
        var nameIndex = 1;

        if (args.Length > 1 && int.TryParse(args[1], out var parsedLevel))
        {
            level = parsedLevel;
            nameIndex = 2;
        }

        if (level < 1 || level > MaxLevel)
        {
            ChatHelper.SendSystemMessage(invoker, $"Level must be between 1 and {MaxLevel}.");
            return true;
        }

        if (!_resourceManager.Profiles.ContainsKey(profileId))
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown profile {profileId}. Use !{KeyWord} list.");
            return true;
        }

        if (!TryGetTarget(invoker, args[nameIndex..], out var target))
            return true;

        if (target.Profiles.Any(x => x.Id == profileId))
        {
            ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName} already has profile {profileId}.");
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters
            .AsNoTrackingWithIdentityResolution()
            .Include(x => x.Items)
            .Include(x => x.Profiles)
                .ThenInclude(x => x.Items)
            .AsSplitQuery()
            .SingleOrDefault(x => x.Id == GuidHelper.GetPlayerId(target.Guid));

        if (dbCharacter is null || !ProfileHelper.AddProfile(dbCharacter, dbContext, _resourceManager, _logger, profileId, level))
        {
            ChatHelper.SendSystemMessage(invoker, $"Failed to grant profile {profileId}. Check the server log.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Grant", target.Name.FullName, $"Profile: {profileId}, Level: {level}");
        ChatHelper.SendSystemMessage(invoker, $"Granted profile {profileId} (level {level}) to {target.Name.FullName}. It appears after relog.");

        return true;
    }

    private bool HandleLevel(Player invoker, string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out var profileId) || !int.TryParse(args[1], out var level))
            return false;

        if (level < 1 || level > MaxLevel)
        {
            ChatHelper.SendSystemMessage(invoker, $"Level must be between 1 and {MaxLevel}.");
            return true;
        }

        if (!TryGetTarget(invoker, args[2..], out var target))
            return true;

        var characterId = GuidHelper.GetPlayerId(target.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var updated = dbContext.Profiles
            .Where(x => x.CharacterId == characterId && x.Id == profileId)
            .ExecuteUpdate(x => x
                .SetProperty(p => p.Level, level)
                .SetProperty(p => p.LevelXP, 0));

        if (updated <= 0)
        {
            ChatHelper.SendSystemMessage(invoker, $"{target.Name.FullName} does not have profile {profileId}.");
            return true;
        }

        var clientPcProfile = target.Profiles.FirstOrDefault(x => x.Id == profileId);

        if (clientPcProfile is not null)
        {
            clientPcProfile.Rank = level;
            clientPcProfile.RankPercent = 0;
        }

        _chatCommandManager.LogAction(this, invoker, "Level", target.Name.FullName, $"Profile: {profileId}, Level: {level}");
        ChatHelper.SendSystemMessage(invoker, $"Profile {profileId} on {target.Name.FullName} set to level {level}. It shows after relog.");

        return true;
    }

    private bool TryGetTarget(Player invoker, string[] nameArgs, out Player target)
    {
        target = invoker;

        if (nameArgs.Length == 0)
            return true;

        var targetName = string.Join(' ', nameArgs);

        if (_zoneManager.TryGetPlayer(targetName, out var found))
        {
            target = found;
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
        return false;
    }
}
