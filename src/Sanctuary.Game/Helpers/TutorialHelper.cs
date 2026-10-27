using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources;

namespace Sanctuary.Game.Helpers;

public enum TutorialSet
{
    /// <summary>
    /// FirstTimeEvents.txt ids (UI tips such as GamedockInventory).
    /// </summary>
    FirstTimeEvents,

    /// <summary>
    /// GameTutorialData.txt ids (minigame tutorial screens).
    /// </summary>
    MiniGameTutorials
}

/// <summary>
/// Keeps a player's seen first-time-event and minigame-tutorial ids in sync with the database.
/// The client only reads these from ClientPcData, so changes made here show after relog.
/// </summary>
public static class TutorialHelper
{
    public static List<int> GetSet(Player player, TutorialSet set)
    {
        return set == TutorialSet.FirstTimeEvents ? player.FirstTimeEvents : player.MiniGameTutorials;
    }

    public static IdToStringLookup GetDefinitions(IResourceManager resourceManager, TutorialSet set)
    {
        return set == TutorialSet.FirstTimeEvents ? resourceManager.FirstTimeEvents : resourceManager.MiniGameTutorials;
    }

    /// <summary>
    /// Marks ids as seen (or unseen). Returns false if nothing changed.
    /// </summary>
    public static bool SetSeen(IDbContextFactory<DatabaseContext> dbContextFactory, Player player, TutorialSet set, IEnumerable<int> ids, bool seen)
    {
        var list = GetSet(player, set);

        List<int> snapshot;

        lock (list)
        {
            var changed = false;

            foreach (var id in ids)
            {
                if (seen && !list.Contains(id))
                {
                    list.Add(id);
                    changed = true;
                }
                else if (!seen)
                {
                    changed |= list.Remove(id);
                }
            }

            if (!changed)
                return false;

            snapshot = list.ToList();
        }

        Save(dbContextFactory, player, set, snapshot);

        return true;
    }

    public static void Clear(IDbContextFactory<DatabaseContext> dbContextFactory, Player player, TutorialSet set)
    {
        var list = GetSet(player, set);

        lock (list)
            list.Clear();

        Save(dbContextFactory, player, set, []);
    }

    private static void Save(IDbContextFactory<DatabaseContext> dbContextFactory, Player player, TutorialSet set, List<int> ids)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        using var dbContext = dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters.SingleOrDefault(x => x.Id == characterId);

        if (dbCharacter is null)
            return;

        if (set == TutorialSet.FirstTimeEvents)
            dbCharacter.FirstTimeEvents = ids;
        else
            dbCharacter.MiniGameTutorials = ids;

        dbContext.SaveChanges();
    }
}
