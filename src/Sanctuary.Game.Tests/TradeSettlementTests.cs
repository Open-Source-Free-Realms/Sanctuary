using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sanctuary.Core.IO;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Database.Sqlite;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class TradeSettlementTests
{
    private sealed class Fault : SaveChangesInterceptor
    {
        public bool FailAfterSave;
        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            if (FailAfterSave) throw new InvalidOperationException("Injected failure after writes, before commit.");
            return result;
        }
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Factory(DbContextOptions options) : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => new SqliteDatabaseContext(options);
    }
    private sealed class Peer(ulong id, object zone) : ITradeParticipant
    {
        public ulong TradeGuid => id;
        public ulong TradeCharacterId => id;
        public int TradeCoins { get; set; } = 100;
        public NameData TradeName { get; } = new() { FirstName = "Player" + id };
        public object TradeContext => zone;
        public bool TradeAvailable { get; set; } = true;
        public bool FailNotifications;
        public bool Disconnected;
        public readonly Dictionary<int, ClientItem> Items = [];
        public readonly Dictionary<int, ClientItemDefinition> Definitions = [];
        public readonly List<ISerializablePacket> Packets = [];
        public bool TryGetTradeItem(int id, out ItemInstance item, out ClientItemDefinition definition, out int available)
        {
            item = null!; definition = null!; available = 0;
            if (!Items.TryGetValue(id, out var owned) || !Definitions.TryGetValue(owned.Definition, out var found)) return false;
            item = owned; definition = found; available = owned.Count - owned.ConsumedCount;
            return true;
        }
        public IReadOnlyList<ISerializablePacket> ApplyTradeResult(TradeParticipantResult result)
        {
            foreach (var change in result.Items)
            {
                if (change.Count == 0) Items.Remove(change.Id);
                else Items[change.Id] = new ClientItem { Id = change.Id, Definition = change.Definition, Tint = change.Tint, Count = change.Count };
            }
            TradeCoins = result.Coins;
            return [new ClientUpdatePacketCoinCount { Coins = TradeCoins }];
        }
        public void SendTunneled(ISerializablePacket packet)
        {
            if (FailNotifications) throw new InvalidOperationException("Disconnected during post-commit delivery.");
            Packets.Add(packet);
        }
        public void DisconnectForTradeResync() { Disconnected = true; TradeAvailable = false; }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        public readonly Fault Fault = new();
        public readonly Factory Factory;
        public readonly Peer A;
        public readonly Peer B;
        public readonly TradeSettlement Settlement;
        public Fixture()
        {
            _connection.Open();
            var options = new DbContextOptionsBuilder<SqliteDatabaseContext>().UseSqlite(_connection).AddInterceptors(Fault).Options;
            Factory = new Factory(options);
            using var db = Factory.CreateDbContext();
            db.Database.EnsureCreated();
            db.Users.Add(new DbUser { Id = 1, Username = "trade-test", Password = "test", Created = DateTimeOffset.UtcNow });
            foreach (var id in new ulong[] { 1, 2 })
                db.Characters.Add(new DbCharacter { Id = id, UserId = 1, FirstName = "Player" + id, Head = "head", HeadId = 1, Hair = "hair", HairId = 1, SkinTone = "skin", SkinToneId = 1, Coins = 100 });
            db.SaveChanges();
            A = new Peer(1, this); B = new Peer(2, this);
            foreach (var peer in new[] { A, B })
                foreach (var definition in new[] { 100, 200 })
                    peer.Definitions[definition] = new ClientItemDefinition { Id = definition, Type = 1, MaxStackSize = 100 };
            Settlement = new TradeSettlement(Factory);
        }
        public void Add(Peer peer, int id, int definition, int count, int tint = 0)
        {
            using var db = Factory.CreateDbContext();
            db.Items.Add(new DbItem { Id = id, CharacterId = peer.TradeCharacterId, Definition = definition, Count = count, Tint = tint });
            db.SaveChanges();
            peer.Items[id] = new ClientItem { Id = id, Definition = definition, Count = count, Tint = tint };
        }
        public List<DbItem> Rows()
        {
            using var db = Factory.CreateDbContext();
            return db.Items.AsNoTracking().AsEnumerable().OrderBy(x => x.CharacterId).ThenBy(x => x.Id).ToList();
        }
        public void Dispose() => _connection.Dispose();
    }
    private static void Fails<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        Assert.Fail("Expected " + typeof(T).Name);
    }

    [TestMethod]
    public void PartialStacksMergeAndCoinsExchangeInOneCommit()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 5); f.Add(f.B, 9, 100, 2);
        var result = f.Settlement.Commit(f.A, f.B, [new(7, 100, 0, 3)], [], 20, 7);
        var rows = f.Rows();
        Assert.AreEqual(2, rows[0].Count); Assert.AreEqual(5, rows[1].Count);
        Assert.AreEqual(9, rows[1].Id);
        Assert.AreEqual(5, f.A.Items[7].Count); // Persistence service never publishes memory before commit.
        Assert.AreEqual(87, result.First.Coins); Assert.AreEqual(113, result.Second.Coins);
        using var reloaded = f.Factory.CreateDbContext();
        Assert.AreEqual(87, reloaded.Characters.Single(x => x.Id == 1).Coins);
        Assert.AreEqual(113, reloaded.Characters.Single(x => x.Id == 2).Coins);
    }

    [TestMethod]
    public void LastCopyDeletesSourceAndAllocatesRecipientIdWithoutMergingDifferentTint()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 1, 5); f.Add(f.B, 7, 100, 2, 6);
        var result = f.Settlement.Commit(f.A, f.B, [new(7, 100, 5, 1)], [], 0, 0);
        var rows = f.Rows();
        Assert.IsFalse(rows.Any(x => x.CharacterId == 1));
        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual(8, rows.Single(x => x.Tint == 5).Id);
        Assert.AreEqual(0, result.First.Items.Single().Count);
        Assert.AreEqual(1, result.Second.Items.Single().Count);
    }

    [TestMethod]
    public void CrossOffersUseNetStackCapacityAndPreserveOwnerIds()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 5); f.Add(f.B, 7, 100, 5);
        f.A.Definitions[100].MaxStackSize = f.B.Definitions[100].MaxStackSize = 5;
        f.Settlement.Commit(f.A, f.B, [new(7, 100, 0, 5)], [new(7, 100, 0, 5)], 0, 0);
        Assert.IsTrue(f.Rows().All(x => x.Id == 7 && x.Count == 5));
    }

    [TestMethod]
    public void InvalidPartnerOfferAndCapacityFailureLeaveBothSidesUnchanged()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 5); f.Add(f.B, 9, 100, 4);
        Fails<InvalidOperationException>(() => f.Settlement.Commit(f.A, f.B, [new(7, 100, 0, 1)], [new(999, 200, 0, 1)], 0, 0));
        f.A.Definitions[100].MaxStackSize = 4;
        Fails<InvalidOperationException>(() => f.Settlement.Commit(f.A, f.B, [new(7, 100, 0, 1)], [], 0, 0));
        Assert.AreEqual(9, f.Rows().Sum(x => x.Count));
        Assert.AreEqual(5, f.Rows()[0].Count);
        Assert.AreEqual(4, f.Rows()[1].Count);
    }

    [TestMethod]
    public void FailureAfterDatabaseWritesRollsBackItemsAndCoins()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 1);
        f.Fault.FailAfterSave = true;
        Fails<InvalidOperationException>(() => f.Settlement.Commit(f.A, f.B, [new(7, 100, 0, 1)], [], 20, 0));
        var rows = f.Rows();
        Assert.AreEqual(1, rows.Count); Assert.AreEqual((ulong)1, rows[0].CharacterId);
        using var db = f.Factory.CreateDbContext();
        Assert.IsTrue(db.Characters.All(x => x.Coins == 100));
    }

    [TestMethod]
    public void StaleInventoryAndEquippedLastCopyCannotTransfer()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 2);
        using (var db = f.Factory.CreateDbContext())
        {
            db.Items.Single().Count = 1; db.SaveChanges();
        }
        Fails<InvalidOperationException>(() => f.Settlement.Commit(f.A, f.B, [new(7, 100, 0, 1)], [], 0, 0));
        f.A.Items[7].Count = 1;
        using (var db = f.Factory.CreateDbContext())
        {
            db.Profiles.Add(new DbProfile { Id = 1, CharacterId = 1, Items = [db.Items.Single()] }); db.SaveChanges();
        }
        Fails<InvalidOperationException>(() => f.Settlement.Commit(f.A, f.B, [new(7, 100, 0, 1)], [], 0, 0));
        Assert.AreEqual(1, f.Rows().Count);
    }

    [TestMethod]
    public void StaleWriterCannotOverwriteCommittedCountsOrCoins()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 5);
        using var stale = f.Factory.CreateDbContext();
        var item = stale.Items.Single();
        var character = stale.Characters.Single(x => x.Id == 1);
        f.Settlement.Commit(f.A, f.B, [new(7, 100, 0, 2)], [], 10, 0);
        item.Count = 4; character.Coins = 999;
        Fails<DbUpdateConcurrencyException>(() => stale.SaveChanges());
        Assert.AreEqual(3, f.Rows().Single(x => x.CharacterId == 1).Count);
        using var db = f.Factory.CreateDbContext();
        Assert.AreEqual(90, db.Characters.Single(x => x.Id == 1).Coins);
    }

    [TestMethod]
    public void DuplicateConcurrentAcceptsCommitOnceAndUpdateBothLiveInventories()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 2);
        var clock = new Clock();
        using var manager = new TradeManager(NullLogger<TradeManager>.Instance, clock, f.Settlement);
        manager.Invite(f.A, f.B);
        var id = f.B.Packets.OfType<TradeInvitePacket>().Single().SessionId;
        manager.Reply(f.B, id, true);
        manager.OfferItem(f.A, id, 7, 1);
        manager.OfferCoins(f.A, id, 10);
        var revision = f.B.Packets.OfType<TradeUpdateCoinCountPacket>().Last().Revision;
        manager.SetLocked(f.A, id, revision, true); manager.SetLocked(f.B, id, revision, true);
        clock.Now += TradeManager.ReviewDelay;
        Parallel.For(0, 20, i => manager.Accept(i % 2 == 0 ? f.A : f.B, id, revision));
        Assert.AreEqual(2, f.Rows().Sum(x => x.Count));
        Assert.AreEqual(1, f.A.Items[7].Count); Assert.AreEqual(1, f.B.Items.Values.Single().Count);
        Assert.AreEqual(90, f.A.TradeCoins); Assert.AreEqual(110, f.B.TradeCoins);
        foreach (var peer in new[] { f.A, f.B })
        {
            var completed = peer.Packets.OfType<TradeMessagePacket>().Single(x => x.MessageCode == 6);
            var ended = peer.Packets.OfType<TradeEndSessionPacket>().Single();
            Assert.IsTrue(peer.Packets.IndexOf(completed) < peer.Packets.IndexOf(ended));
        }
    }

    [TestMethod]
    public void CoinsOnlyTradePersistsAndInsufficientOrOverflowingBalancesRollBack()
    {
        using var f = new Fixture();
        var result = f.Settlement.Commit(f.A, f.B, [], [], 25, 10);
        Assert.AreEqual(85, result.First.Coins); Assert.AreEqual(115, result.Second.Coins);
        f.A.TradeCoins = 85; f.B.TradeCoins = 115;
        Fails<TradeValidationException>(() => f.Settlement.Commit(f.A, f.B, [], [], 86, 0));
        Fails<TradeValidationException>(() => f.Settlement.Commit(f.A, f.B, [], [], -1, 0));
        using (var db = f.Factory.CreateDbContext())
        {
            db.Characters.Single(x => x.Id == 2).Coins = int.MaxValue; db.SaveChanges();
        }
        f.B.TradeCoins = int.MaxValue;
        Fails<OverflowException>(() => f.Settlement.Commit(f.A, f.B, [], [], 1, 0));
        using var verify = f.Factory.CreateDbContext();
        Assert.AreEqual(85, verify.Characters.Single(x => x.Id == 1).Coins);
        Assert.AreEqual(int.MaxValue, verify.Characters.Single(x => x.Id == 2).Coins);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SaleBeforeFinalAcceptanceInvalidatesOffer(bool updateLiveInventory)
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 2);
        var clock = new Clock();
        using var manager = new TradeManager(NullLogger<TradeManager>.Instance, clock, f.Settlement);
        manager.Invite(f.A, f.B); var id = f.B.Packets.OfType<TradeInvitePacket>().Single().SessionId;
        manager.Reply(f.B, id, true); manager.OfferItem(f.A, id, 7, 2);
        manager.SetLocked(f.A, id, 2, true); manager.SetLocked(f.B, id, 2, true);
        clock.Now += TradeManager.ReviewDelay; manager.Accept(f.A, id, 2);
        // A sale has committed before the second acceptance. Check both a fully
        // published sale and a database change whose live update is still pending.
        using (var db = f.Factory.CreateDbContext())
        {
            db.Items.Single().Count = 1;
            db.SaveChanges();
        }
        if (updateLiveInventory)
            f.A.Items[7].Count = 1;
        manager.Accept(f.B, id, 2);
        Assert.AreEqual(1, f.Rows().Single().Count);
        Assert.AreEqual((ulong)1, f.Rows().Single().CharacterId);
        Assert.AreEqual(0, f.B.Items.Count);
        foreach (var peer in new[] { f.A, f.B })
        {
            Assert.AreEqual(0, peer.Packets.OfType<TradeMessagePacket>().Count());
            Assert.AreEqual(1, peer.Packets.OfType<TradeEndSessionPacket>().Count());
            Assert.IsTrue(peer.Packets.OfType<ExecuteScriptWithStringParamsPacket>()
                .Any(packet => packet.Params[0].StartsWith("Trade could not be completed.")));
        }
        Assert.IsTrue(manager.Invite(f.A, f.B));
    }

    [TestMethod]
    public void ConcurrencyTokensDoNotRequireDatabaseSchemaMigration()
    {
        using var f = new Fixture();
        using var db = f.Factory.CreateDbContext();
        Assert.IsFalse(db.Database.HasPendingModelChanges());
    }

    [TestMethod]
    public void FailedPostCommitNotificationForcesReloadWithoutReplayingExchange()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 1);
        var clock = new Clock();
        using var manager = new TradeManager(NullLogger<TradeManager>.Instance, clock, f.Settlement);
        manager.Invite(f.A, f.B); var id = f.B.Packets.OfType<TradeInvitePacket>().Single().SessionId;
        manager.Reply(f.B, id, true); manager.OfferItem(f.A, id, 7, 1);
        manager.SetLocked(f.A, id, 2, true); manager.SetLocked(f.B, id, 2, true);
        clock.Now += TradeManager.ReviewDelay;
        manager.Accept(f.A, id, 2);
        f.A.FailNotifications = true;
        manager.Accept(f.B, id, 2); manager.Accept(f.B, id, 2);
        Assert.IsTrue(f.A.Disconnected);
        Assert.AreEqual(0, f.A.Items.Count); Assert.AreEqual(1, f.B.Items.Count);
        Assert.AreEqual((ulong)2, f.Rows().Single().CharacterId);
    }
}
