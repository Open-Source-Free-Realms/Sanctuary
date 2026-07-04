using System.Collections.Concurrent;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;

namespace Sanctuary.Gateway.Fishing;

/// <summary>
/// Registry of active <see cref="FishingSession"/>s keyed by player guid, plus the periodic tick
/// that drives their bite timelines. Ticked from the gateway main loop (see GatewayService).
/// </summary>
public static class FishingSessions
{
    private static readonly ConcurrentDictionary<ulong, FishingSession> Sessions = new();

    /// <summary>Shared logger for fishing sessions (set from a fishing packet handler at startup).</summary>
    public static ILogger? Logger { get; set; }

    /// <summary>DB access for persisting caught items (set from a fishing packet handler at startup).</summary>
    public static IDbContextFactory<DatabaseContext>? DbContextFactory { get; set; }

    /// <summary>
    /// Grants a caught item to the player AND persists it immediately (crash-safe, like the coin store),
    /// so the catch survives logout. The DB row's id is used as the in-memory item id so the two stay in
    /// sync (other systems — selling, equipping — match items by that id). Falls back to an in-memory-only
    /// grant if the DB is unavailable. Returns true if the item was granted.
    /// </summary>
    public static bool GrantCaughtItem(Player player, int definitionId, int count = 1)
    {
        if (DbContextFactory is not null)
        {
            try
            {
                var characterId = GuidHelper.GetPlayerId(player.Guid);

                // DbItem's key is per-character (Id, CharacterId) and is NOT auto-generated, so we must
                // assign the next free id ourselves — leaving it 0 collides ("UNIQUE constraint failed").
                // Player.Items already mirrors the character's rows (loaded on login), so max+1 is free in
                // both the bag and the DB and keeps ClientItem.Id == DbItem.Id (what selling/equipping match on).
                var nextId = player.Items.Count == 0 ? 1 : player.Items.Max(x => x.Id) + 1;

                using var db = DbContextFactory.CreateDbContext();

                db.Items.Add(new DbItem
                {
                    Id = nextId,
                    CharacterId = characterId,
                    Definition = definitionId,
                    Count = count,
                    Tint = 0
                });

                if (db.SaveChanges() > 0)
                    return player.GiveItem(definitionId, count, nextId);
            }
            catch (System.Exception ex)
            {
                Logger?.LogError(ex, "Failed to persist caught item (def {def}); granting in-memory only", definitionId);
            }
        }

        // No DB (or it failed): grant in-memory so the catch still shows this session.
        return player.GiveItem(definitionId, count);
    }

    /// <summary>
    /// Consumes one of a SingleUse item (e.g. an activated fishing lure): decrements the in-memory bag,
    /// notifies the client, and persists the change. No-op if the player has none of it.
    /// </summary>
    public static void ConsumeItem(Player player, int definitionId)
    {
        var clientItem = player.Items.FirstOrDefault(x => x.Definition == definitionId);
        if (clientItem is null)
            return;

        if (clientItem.Count > 1)
        {
            clientItem.Count -= 1;
            player.SendTunneled(new Sanctuary.Packet.ClientUpdatePacketItemUpdate
            {
                ItemGuid = clientItem.Id,
                Count = clientItem.Count
            });
        }
        else
        {
            player.Items.Remove(clientItem);
            player.SendTunneled(new Sanctuary.Packet.ClientUpdatePacketItemDelete { ItemGuid = clientItem.Id });
        }

        if (DbContextFactory is null)
            return;

        try
        {
            var characterId = GuidHelper.GetPlayerId(player.Guid);

            using var db = DbContextFactory.CreateDbContext();

            var dbItem = db.Items.SingleOrDefault(x => x.CharacterId == characterId && x.Id == clientItem.Id);
            if (dbItem is not null)
            {
                if (dbItem.Count > 1)
                    dbItem.Count -= 1;
                else
                    db.Items.Remove(dbItem);

                db.SaveChanges();
            }
        }
        catch (System.Exception ex)
        {
            Logger?.LogError(ex, "Failed to persist item consumption (def {def})", definitionId);
        }
    }

    public static FishingSession GetOrCreate(Player player) =>
        Sessions.GetOrAdd(player.Guid, _ => new FishingSession(player));

    public static bool TryGet(ulong playerGuid, out FishingSession session) =>
        Sessions.TryGetValue(playerGuid, out session!);

    public static void Remove(ulong playerGuid) => Sessions.TryRemove(playerGuid, out _);

    /// <summary>Advances every active session's bite timeline. Cheap when sessions are idle.</summary>
    public static void Tick()
    {
        foreach (var session in Sessions.Values)
            session.Update();
    }
}
