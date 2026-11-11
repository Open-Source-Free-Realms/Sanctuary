using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
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
    private sealed class TransientFailure : Exception;
    private sealed class RetryingStrategy(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, 2, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TransientFailure;
    }
    private sealed class RetryingStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create() => new RetryingStrategy(dependencies);
    }
    private sealed class CommitFault : DbTransactionInterceptor
    {
        public bool LoseNextCommitResponse;
        public int Commits;
        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            Commits++;
            if (LoseNextCommitResponse)
            {
                LoseNextCommitResponse = false;
                throw new TransientFailure();
            }
        }
    }
    private sealed class Fault : SaveChangesInterceptor
    {
        public bool FailAfterSave;
        public int TransientFailures;
        public readonly HashSet<DbContext> SavedContexts = [];
        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            SavedContexts.Add(eventData.Context!);
            if (TransientFailures > 0)
            {
                TransientFailures--;
                throw new TransientFailure();
            }
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
        public readonly CommitFault CommitFault = new();
        public readonly Factory Factory;
        public readonly Peer A;
        public readonly Peer B;
        public readonly TradeSettlement Settlement;
        public Fixture(bool retries = false)
        {
            _connection.Open();
            var builder = new DbContextOptionsBuilder<SqliteDatabaseContext>().UseSqlite(_connection)
                .AddInterceptors(Fault, CommitFault);
            if (retries)
                builder.ReplaceService<IExecutionStrategyFactory, RetryingStrategyFactory>();
            Factory = new Factory(builder.Options);
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
    [DataRow(0)]
    [DataRow(1)]
    public void RetryingStrategyRunsTransactionAndRetriesRolledBackWritesWithFreshContext(int failures)
    {
        using var f = new Fixture(retries: true);
        f.Add(f.A, 7, 100, 5);
        f.Fault.SavedContexts.Clear();
        f.Fault.TransientFailures = failures;
        var result = f.Settlement.Commit(new(f.A, [new(7, 100, 0, 2)], 10), new(f.B, [], 0));
        Assert.AreEqual(failures + 1, f.Fault.SavedContexts.Count);
        Assert.AreEqual(3, f.Rows().Single(x => x.CharacterId == 1).Count);
        Assert.AreEqual(2, f.Rows().Single(x => x.CharacterId == 2).Count);
        Assert.AreEqual(90, result.First.Coins);
        Assert.AreEqual(110, result.Second.Coins);
        Assert.AreEqual(5, f.A.Items[7].Count);
        Assert.AreEqual(0, f.B.Items.Count);
    }

    [TestMethod]
    public void LostCommitResponseDoesNotReplayOrPublishAndDisconnectsBothPlayers()
    {
        using var f = new Fixture(retries: true);
        f.Add(f.A, 7, 100, 5);
        var clock = new Clock();
        using var manager = new TradeManager(NullLogger<TradeManager>.Instance, clock, f.Settlement);
        manager.Invite(f.A, f.B);
        var id = f.B.Packets.OfType<TradeInvitePacket>().Single().SessionId;
        manager.Reply(f.B, id, true);
        manager.OfferItem(f.A, id, 7, 2);
        manager.OfferCoins(f.A, id, 10);
        var revision = f.B.Packets.OfType<TradeUpdateCoinCountPacket>().Last().Revision;
        manager.SetLocked(f.A, id, revision, true); manager.SetLocked(f.B, id, revision, true);
        manager.Accept(f.A, id, revision);
        f.Fault.SavedContexts.Clear();
        f.CommitFault.Commits = 0;
        f.CommitFault.LoseNextCommitResponse = true;
        manager.Accept(f.B, id, revision);
        manager.Accept(f.B, id, revision);
        Assert.AreEqual(1, f.CommitFault.Commits);
        Assert.AreEqual(1, f.Fault.SavedContexts.Count);
        Assert.AreEqual(3, f.Rows().Single(x => x.CharacterId == 1).Count);
        Assert.AreEqual(2, f.Rows().Single(x => x.CharacterId == 2).Count);
        using var db = f.Factory.CreateDbContext();
        Assert.AreEqual(90, db.Characters.Single(x => x.Id == 1).Coins);
        Assert.AreEqual(110, db.Characters.Single(x => x.Id == 2).Coins);
        Assert.AreEqual(5, f.A.Items[7].Count);
        Assert.AreEqual(0, f.B.Items.Count);
        Assert.IsTrue(f.A.Disconnected && f.B.Disconnected);
        Assert.IsFalse(f.A.Packets.Concat(f.B.Packets).OfType<TradeMessagePacket>().Any(x => x.MessageCode == 6));
    }

    [TestMethod]
    public void PartialStacksMergeAndCoinsExchangeInOneCommit()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 5); f.Add(f.B, 9, 100, 2);
        var result = f.Settlement.Commit(new(f.A, [new(7, 100, 0, 3)], 20), new(f.B, [], 7));
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
        var result = f.Settlement.Commit(new(f.A, [new(7, 100, 5, 1)], 0), new(f.B, [], 0));
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
        f.Settlement.Commit(new(f.A, [new(7, 100, 0, 5)], 0), new(f.B, [new(7, 100, 0, 5)], 0));
        Assert.IsTrue(f.Rows().All(x => x.Id == 7 && x.Count == 5));
    }

    [TestMethod]
    public void InvalidPartnerOfferAndCapacityFailureLeaveBothSidesUnchanged()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 5); f.Add(f.B, 9, 100, 4);
        Fails<InvalidOperationException>(() => f.Settlement.Commit(new(f.A, [new(7, 100, 0, 1)], 0), new(f.B, [new(999, 200, 0, 1)], 0)));
        f.A.Definitions[100].MaxStackSize = 4;
        Fails<InvalidOperationException>(() => f.Settlement.Commit(new(f.A, [new(7, 100, 0, 1)], 0), new(f.B, [], 0)));
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
        Fails<InvalidOperationException>(() => f.Settlement.Commit(new(f.A, [new(7, 100, 0, 1)], 20), new(f.B, [], 0)));
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
        Fails<InvalidOperationException>(() => f.Settlement.Commit(new(f.A, [new(7, 100, 0, 1)], 0), new(f.B, [], 0)));
        f.A.Items[7].Count = 1;
        using (var db = f.Factory.CreateDbContext())
        {
            db.Profiles.Add(new DbProfile { Id = 1, CharacterId = 1, Items = [db.Items.Single()] }); db.SaveChanges();
        }
        Fails<InvalidOperationException>(() => f.Settlement.Commit(new(f.A, [new(7, 100, 0, 1)], 0), new(f.B, [], 0)));
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
        f.Settlement.Commit(new(f.A, [new(7, 100, 0, 2)], 10), new(f.B, [], 0));
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
        var result = f.Settlement.Commit(new(f.A, [], 25), new(f.B, [], 10));
        Assert.AreEqual(85, result.First.Coins); Assert.AreEqual(115, result.Second.Coins);
        f.A.TradeCoins = 85; f.B.TradeCoins = 115;
        Fails<TradeValidationException>(() => f.Settlement.Commit(new(f.A, [], 86), new(f.B, [], 0)));
        Fails<TradeValidationException>(() => f.Settlement.Commit(new(f.A, [], -1), new(f.B, [], 0)));
        using (var db = f.Factory.CreateDbContext())
        {
            db.Characters.Single(x => x.Id == 2).Coins = int.MaxValue; db.SaveChanges();
        }
        f.B.TradeCoins = int.MaxValue;
        Fails<OverflowException>(() => f.Settlement.Commit(new(f.A, [], 1), new(f.B, [], 0)));
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
        manager.Accept(f.A, id, 2);
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
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(8)]
    public void SettlementReservesPersistedPetEquipmentOncePerPet(int slot)
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 2);
        using (var db = f.Factory.CreateDbContext())
        {
            var pet = new DbPet { Id = 3, CharacterId = 1, Definition = 200 };
            switch (slot)
            {
                case -1: pet.Items.Add(db.Items.Single()); break;
                case 0: pet.ShampooItemId = 7; break;
                case 1: pet.ConditionerItemId = 7; break;
                case 2: pet.ToyItemId = 7; break;
                case 3: pet.BrushItemId = 7; break;
                case 4: pet.FoodBowlItemId = 7; break;
                case 5: pet.FoodItemId = 7; break;
                case 6: pet.VitaminItemId = 7; break;
                case 8: pet.Items.Add(db.Items.Single()); pet.ShampooItemId = pet.ToyItemId = 7; break;
            }
            db.Pets.Add(pet);
            db.SaveChanges();
        }
        Fails<TradeValidationException>(() => f.Settlement.Commit(new(f.A, [new(7, 100, 0, 2)], 0), new(f.B, [], 0)));
        f.Settlement.Commit(new(f.A, [new(7, 100, 0, 1)], 0), new(f.B, [], 0));
        Assert.AreEqual(1, f.Rows().Single(x => x.CharacterId == 1).Count);
        Assert.AreEqual(1, f.Rows().Single(x => x.CharacterId == 2).Count);
    }

    [TestMethod]
    public void MissingDefinitionBeforeAcceptanceRejectsWithoutTransferOrDisconnect()
    {
        using var f = new Fixture();
        f.Add(f.A, 7, 100, 2);
        using var manager = new TradeManager(NullLogger<TradeManager>.Instance, settlement: f.Settlement);
        manager.Invite(f.A, f.B);
        var id = f.B.Packets.OfType<TradeInvitePacket>().Single().SessionId;
        manager.Reply(f.B, id, true); manager.OfferItem(f.A, id, 7, 1);
        manager.SetLocked(f.A, id, 2, true); manager.SetLocked(f.B, id, 2, true);
        f.A.Definitions.Clear();
        manager.Accept(f.A, id, 2); manager.Accept(f.B, id, 2);
        Assert.AreEqual(2, f.Rows().Single().Count);
        Assert.AreEqual((ulong)1, f.Rows().Single().CharacterId);
        Assert.IsFalse(f.A.Disconnected || f.B.Disconnected);
        Assert.AreEqual(1, f.B.Packets.OfType<TradeEndSessionPacket>().Count());
        Assert.IsFalse(f.B.Packets.OfType<TradeMessagePacket>().Any(x => x.MessageCode == TradeMessagePacket.Completed));
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
        manager.Accept(f.A, id, 2);
        f.A.FailNotifications = true;
        manager.Accept(f.B, id, 2); manager.Accept(f.B, id, 2);
        Assert.IsTrue(f.A.Disconnected);
        Assert.IsFalse(f.B.Disconnected);
        Assert.AreEqual(0, f.A.Items.Count); Assert.AreEqual(1, f.B.Items.Count);
        Assert.AreEqual((ulong)2, f.Rows().Single().CharacterId);
    }
}
