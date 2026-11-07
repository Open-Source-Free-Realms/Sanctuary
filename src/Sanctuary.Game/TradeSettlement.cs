using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game;

// No client notification or in-memory mutation happens until Commit returns successfully.
// The transaction and concurrency tokens protect database writes, not live inventory
// access by other gameplay code. That still needs zone-owned packet marshalling.
public sealed class TradeSettlement(IDbContextFactory<DatabaseContext> factory) : ITradeSettlement
{
    public TradeSettlementResult Commit(ITradeParticipant first, ITradeParticipant second,
        IReadOnlyCollection<TradeItemOffer> firstItems, IReadOnlyCollection<TradeItemOffer> secondItems,
        int firstCoins, int secondCoins)
    {
        if (!first.TradeAvailable || !second.TradeAvailable || first.TradeCharacterId == second.TradeCharacterId)
            throw new TradeValidationException("A trade participant is unavailable.");
        using var db = factory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
        var firstId = first.TradeCharacterId;
        var secondId = second.TradeCharacterId;
        var characters = new[] { firstId, secondId }.Order().Select(id => db.Characters.Single(x => x.Id == id)).ToList();
        var a = characters.Single(x => x.Id == firstId);
        var b = characters.Single(x => x.Id == secondId);
        var rows = db.Items.Where(x => x.CharacterId == firstId || x.CharacterId == secondId)
            .Include(x => x.Profiles).Include(x => x.Pets).AsSplitQuery().ToList();
        var pets = db.Pets.Where(x => x.CharacterId == firstId || x.CharacterId == secondId).ToList();
        var original = rows.ToDictionary(x => (x.CharacterId, x.Id), x => new TradeInventoryChange(x.Id, x.Definition, x.Tint, x.Count));
        var nextIds = characters.ToDictionary(x => x.Id, x => rows.Where(i => i.CharacterId == x.Id).Select(i => i.Id).DefaultIfEmpty(0).Max());
        var maxStacks = new Dictionary<(int, int), int>();

        void Validate(ITradeParticipant owner, IReadOnlyCollection<TradeItemOffer> offers)
        {
            if (offers.Select(x => x.ItemId).Distinct().Count() != offers.Count)
                throw new TradeValidationException("Duplicate item in trade offer.");
            foreach (var offer in offers)
            {
                var row = rows.SingleOrDefault(x => x.CharacterId == owner.TradeCharacterId && x.Id == offer.ItemId);
                if (row is null || offer.Quantity <= 0 || row.Definition != offer.Definition || row.Tint != offer.Tint
                    || !owner.TryGetTradeItem(offer.ItemId, out var live, out var definition, out var available)
                    || live.Definition != row.Definition || live.Tint != row.Tint || live.Count != row.Count
                    || definition.NoTrade || !SupportsItemType(definition.Type) || live.AbilityCount != 0
                    || available < offer.Quantity)
                    throw new TradeValidationException("An offered item changed or is no longer tradable.");
                var reserved = row.Profiles.Count > 0 ? 1 : 0;
                reserved += pets.Count(p => p.CharacterId == row.CharacterId && (row.Pets.Any(r => r.Id == p.Id)
                    || p.ShampooItemId == row.Id || p.ConditionerItemId == row.Id || p.ToyItemId == row.Id
                    || p.BrushItemId == row.Id || p.FoodBowlItemId == row.Id || p.FoodItemId == row.Id || p.VitaminItemId == row.Id));
                if ((long)row.Count - Math.Max(0, live.ConsumedCount) - reserved < offer.Quantity)
                    throw new TradeValidationException("An offered item is equipped or unavailable.");
                maxStacks[(row.Definition, row.Tint)] = definition.MaxStackSize;
            }
        }
        Validate(first, firstItems);
        Validate(second, secondItems);
        if (firstCoins < 0 || secondCoins < 0 || a.Coins != first.TradeCoins || b.Coins != second.TradeCoins
            || a.Coins < firstCoins || b.Coins < secondCoins)
            throw new TradeValidationException("A coin balance changed or is insufficient.");
        var finalA = checked(a.Coins - firstCoins + secondCoins);
        var finalB = checked(b.Coins - secondCoins + firstCoins);

        // Apply both outgoing sides first, then merge incoming quantities by definition/tint.
        // This supports cross-offers of the same stack without transient capacity failures.
        foreach (var (owner, offers) in new[] { (firstId, firstItems), (secondId, secondItems) })
            foreach (var offer in offers)
                rows.Single(x => x.CharacterId == owner && x.Id == offer.ItemId).Count -= offer.Quantity;
        foreach (var (recipient, offers) in new[] { (secondId, firstItems), (firstId, secondItems) })
            foreach (var offer in offers)
            {
                var row = rows.SingleOrDefault(x => x.CharacterId == recipient && x.Definition == offer.Definition && x.Tint == offer.Tint);
                if (row is null)
                {
                    row = new DbItem { Id = checked(++nextIds[recipient]), CharacterId = recipient,
                        Definition = offer.Definition, Tint = offer.Tint, Count = 0 };
                    rows.Add(row);
                    db.Items.Add(row);
                }
                row.Count = checked(row.Count + offer.Quantity);
            }

        var changes = new Dictionary<ulong, List<TradeInventoryChange>> { [firstId] = [], [secondId] = [] };
        foreach (var row in rows)
        {
            original.TryGetValue((row.CharacterId, row.Id), out var before);
            if (before is not null && before.Count == row.Count)
                continue;
            var peer = row.CharacterId == firstId ? first : second;
            // Recipient stacks must also still match the live state before publishing deltas.
            if (before is null && peer.TryGetTradeItem(row.Id, out _, out _, out _))
                throw new TradeValidationException("Recipient inventory needs to be reloaded.");
            if (before is not null && (!peer.TryGetTradeItem(row.Id, out var live, out _, out _)
                || live.Count != before.Count || live.Definition != row.Definition || live.Tint != row.Tint))
                throw new TradeValidationException("Inventory changed; reopen the trade.");
            if (row.Count < 0 || (maxStacks.TryGetValue((row.Definition, row.Tint), out var limit) && limit > 0 && row.Count > limit))
                throw new TradeValidationException("The recipient's stack would exceed its limit.");
            if (row.Count == 0)
            {
                if (row.Profiles.Count != 0 || row.Pets.Count != 0)
                    throw new TradeValidationException("Cannot remove an equipped item.");
                db.Items.Remove(row);
            }
            changes[row.CharacterId].Add(new(row.Id, row.Definition, row.Tint, row.Count));
        }
        a.Coins = finalA;
        b.Coins = finalB;
        db.SaveChanges();
        transaction.Commit();
        return new(new(changes[firstId], finalA), new(changes[secondId], finalB));
    }

    public static bool SupportsItemType(int type) => type != ItemDefinitionHelper.PetItemType
        && type != ItemDefinitionHelper.MountItemType && type != ItemDefinitionHelper.HousingItemType;
}
