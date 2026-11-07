using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class TradeManagerTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Peer(ulong guid, object context) : ITradeParticipant
    {
        public ulong TradeGuid => guid;
        public ulong TradeCharacterId => guid;
        public int TradeCoins { get; set; }
        public bool Disconnected;
        public IReadOnlyList<ISerializablePacket> ApplyTradeResult(TradeParticipantResult result)
        {
            TradeCoins = result.Coins;
            foreach (var change in result.Items)
            {
                if (change.Count == 0) Items.Remove(change.Id);
                else if (Items.TryGetValue(change.Id, out var old)) old.Item.Count = change.Count;
                else Items[change.Id] = (new ClientItem { Id = change.Id, Definition = change.Definition, Tint = change.Tint, Count = change.Count }, new ClientItemDefinition(), change.Count);
            }
            return [];
        }
        public void DisconnectForTradeResync() { Disconnected = true; TradeAvailable = false; }
        public NameData TradeName { get; } = new() { FirstName = "Player" + guid };
        public object TradeContext { get; set; } = context;
        public bool TradeAvailable { get; set; } = true;
        public List<ISerializablePacket> Packets { get; } = [];
        public readonly Dictionary<int, (ItemInstance Item, ClientItemDefinition Definition, int Available)> Items = [];
        public bool TryGetTradeItem(int id, out ItemInstance item, out ClientItemDefinition definition, out int available)
        {
            if (Items.TryGetValue(id, out var entry))
            {
                (item, definition, available) = entry;
                return true;
            }
            item = null!; definition = null!; available = 0;
            return false;
        }
        public void SendTunneled(ISerializablePacket packet) => Packets.Add(packet);
    }

    private static TradeManager Create(Clock? clock = null) => new(NullLogger<TradeManager>.Instance, clock);
    private static int Invitation(Peer peer) => peer.Packets.OfType<TradeInvitePacket>().Last().SessionId;

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CancellationNotifiesOnlyPartnerOnce(bool inviterCancels)
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        trades.Invite(a, b);
        var id = Invitation(b);
        trades.Reply(b, id, true);
        a.Packets.Clear(); b.Packets.Clear();
        var cancelling = inviterCancels ? a : b;
        var partner = inviterCancels ? b : a;
        cancelling.TradeName.FirstName = "<Player&Name>";
        trades.Cancel(new Peer(cancelling.TradeGuid, this), id);
        Assert.AreEqual(0, partner.Packets.Count);
        trades.Cancel(cancelling, id);
        trades.Cancel(cancelling, id);
        Assert.AreEqual(0, cancelling.Packets.OfType<ExecuteScriptWithStringParamsPacket>().Count());
        var notification = partner.Packets.OfType<ExecuteScriptWithStringParamsPacket>().Single();
        Assert.AreEqual("NotificationHandler.SystemMessage", notification.Script);
        CollectionAssert.AreEqual(new[] { "&lt;Player&amp;Name&gt; has cancelled the trade.", "0", "0xFFFF00" }, notification.Params);
        Assert.AreEqual(1, a.Packets.OfType<TradeEndSessionPacket>().Count());
        Assert.AreEqual(1, b.Packets.OfType<TradeEndSessionPacket>().Count());
    }

    [TestMethod]
    public void UnsupportedActionClosureDoesNotClaimPartnerCancelled()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        trades.Invite(a, b);
        var id = Invitation(b);
        trades.Reply(b, id, true);
        a.Packets.Clear(); b.Packets.Clear();
        trades.Cancel(a, id, notifyPartner: false);
        Assert.AreEqual(0, b.Packets.OfType<ExecuteScriptWithStringParamsPacket>().Count());
        Assert.AreEqual(1, b.Packets.OfType<TradeEndSessionPacket>().Count());
    }

    [TestMethod]
    public void AcceptanceStartsBothClientsWithOppositePartnerAndSameSession()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        Assert.IsTrue(trades.Invite(a, b));
        var feedback = a.Packets.OfType<ExecuteScriptWithStringParamsPacket>().Single();
        Assert.AreEqual("NotificationHandler.SystemMessage", feedback.Script);
        CollectionAssert.AreEqual(new[] { "Sent Player2 a trade request.", "0", "0xFFFF00" }, feedback.Params);
        var invite = b.Packets.OfType<TradeInvitePacket>().Single();
        Assert.AreEqual(a.TradeGuid, invite.InviterGuid);
        trades.Reply(b, invite.SessionId, true);
        var first = a.Packets.OfType<TradeStartSessionPacket>().Single();
        var second = b.Packets.OfType<TradeStartSessionPacket>().Single();
        Assert.AreEqual(b.TradeGuid, first.PartnerGuid);
        Assert.AreEqual(a.TradeGuid, second.PartnerGuid);
        Assert.AreSame(b.TradeName, first.PartnerName);
        Assert.AreSame(a.TradeName, second.PartnerName);
        Assert.AreEqual(invite.SessionId, first.SessionId);
        Assert.AreEqual(first.SessionId, second.SessionId);
        trades.Reply(b, invite.SessionId, true);
        Assert.AreEqual(1, a.Packets.OfType<TradeStartSessionPacket>().Count());
        trades.Cancel(a, invite.SessionId);
        Assert.AreEqual(1, b.Packets.OfType<TradeEndSessionPacket>().Count());
        Assert.IsTrue(trades.Invite(b, a));
    }

    [TestMethod]
    public void WrongConnectionCannotReplyOrCancelEvenWithSameGuid()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        var impostor = new Peer(2, this);
        trades.Invite(a, b);
        var id = Invitation(b);
        trades.Reply(a, id, true);
        trades.Reply(impostor, id, true);
        trades.Cancel(impostor, id);
        trades.Disconnect(impostor);
        Assert.AreEqual(1, a.Packets.Count); // Only the original sent-request message.
        Assert.IsFalse(trades.Invite(a, new Peer(3, this)));
        trades.Reply(b, id, true);
        Assert.AreEqual(1, a.Packets.OfType<TradeStartSessionPacket>().Count());
    }

    [TestMethod]
    public void DeclineReleasesBothPlayersAndOldReplyDoesNotStartNewTrade()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        trades.Invite(a, b);
        var old = Invitation(b);
        trades.Reply(b, old, false);
        Assert.AreSame(b.TradeName, a.Packets.OfType<TradeRejectedPacket>().Single().RejectingPlayer);
        Assert.IsTrue(trades.Invite(a, b));
        Assert.AreNotEqual(old, Invitation(b));
        trades.Reply(b, old, true);
        trades.Cancel(a, old);
        Assert.AreEqual(0, a.Packets.OfType<TradeStartSessionPacket>().Count());
        trades.Reply(b, Invitation(b), true);
        Assert.AreEqual(1, a.Packets.OfType<TradeStartSessionPacket>().Count());
    }

    [TestMethod]
    public void ExpiredInvitationCannotBeAccepted()
    {
        var clock = new Clock();
        using var trades = Create(clock);
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        trades.Invite(a, b);
        var id = Invitation(b);
        clock.Now += TradeManager.InvitationLifetime;
        trades.Reply(b, id, true);
        Assert.AreEqual(0, a.Packets.OfType<TradeStartSessionPacket>().Count());
        Assert.AreEqual(id, b.Packets.OfType<TradeEndSessionPacket>().Single().SessionId);
        Assert.IsTrue(trades.Invite(a, b));
    }

    [TestMethod]
    public void BusySelfOfflineAndDifferentZoneRequestsAreRejected()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        Assert.IsFalse(trades.Invite(a, a));
        Assert.IsFalse(trades.Invite(a, new Peer(3, new object())));
        Assert.IsFalse(trades.Invite(a, new Peer(3, this) { TradeAvailable = false }));
        Assert.IsTrue(trades.Invite(a, b));
        Assert.IsFalse(trades.Invite(b, a));
        Assert.IsFalse(trades.Invite(new Peer(3, this), b));
        Assert.AreEqual(1, b.Packets.Count);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(2, false)]
    [DataRow(3, false)]
    [DataRow(0, true)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(3, true)]
    public void DisconnectNotifiesPartnerAndRejectsLateAcceptanceInEveryPhase(int phase, bool sweep)
    {
        var clock = new Clock();
        using var trades = Create(clock);
        var a = new Peer(1, this); var b = new Peer(2, this); var c = new Peer(3, this);
        trades.Invite(a, b); var id = Invitation(b);
        if (phase >= 1) trades.Reply(b, id, true);
        if (phase >= 2) { trades.SetLocked(a, id, 1, true); trades.SetLocked(b, id, 1, true); }
        if (phase >= 3) { clock.Now += TradeManager.ReviewDelay; trades.Accept(b, id, 1); }
        a.Packets.Clear(); b.Packets.Clear();
        a.TradeAvailable = false;
        if (sweep) trades.Sweep(); else trades.Disconnect(a);
        trades.Disconnect(a); trades.Sweep();
        Assert.AreEqual(2, b.Packets.Count);
        Assert.AreEqual((byte)2, ((TradeMessagePacket)b.Packets[0]).MessageCode);
        Assert.IsInstanceOfType<TradeEndSessionPacket>(b.Packets[1]);
        Assert.AreEqual(0, a.Packets.Count);
        Assert.IsTrue(trades.Invite(c, b));
        var next = Invitation(b);
        trades.Reply(b, next, true);
        var count = b.Packets.Count;
        trades.Reply(b, id, true); trades.Accept(a, id, 1); trades.Accept(b, id, 1);
        Assert.AreEqual(count, b.Packets.Count);
        Assert.AreEqual(0, b.Packets.OfType<TradeMessagePacket>().Count(x => x.MessageCode == 6));
    }

    [TestMethod]
    public void BusyPlayerCannotAcceptOldInvitationIntoASecondTrade()
    {
        using var trades = Create();
        var a = new Peer(1, this); var b = new Peer(2, this); var c = new Peer(3, this);
        trades.Invite(c, b); var old = Invitation(b); trades.Cancel(c, old);
        trades.Invite(a, b); var current = Invitation(b); trades.Reply(b, current, true);
        a.Packets.Clear(); b.Packets.Clear(); c.Packets.Clear();
        Assert.IsFalse(trades.Invite(c, b));
        trades.Reply(b, old, true);
        Assert.AreEqual(0, a.Packets.Count); Assert.AreEqual(0, b.Packets.Count); Assert.AreEqual(0, c.Packets.Count);
        trades.SetLocked(a, current, 1, true); trades.SetLocked(b, current, 1, true);
        Assert.AreEqual(current, b.Packets.OfType<TradeConfirmPacket>().Single().SessionId);
    }

    [TestMethod]
    public void DisconnectAndZoneChangeCloseSessionAndReleasePartner()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        trades.Invite(a, b);
        trades.Reply(b, Invitation(b), true);
        a.TradeAvailable = false;
        trades.Disconnect(a);
        Assert.AreEqual(1, b.Packets.OfType<TradeEndSessionPacket>().Count());
        var c = new Peer(3, this);
        Assert.IsTrue(trades.Invite(b, c));
        trades.Reply(c, Invitation(c), true);
        c.TradeContext = new object();
        trades.Sweep();
        Assert.AreEqual(2, b.Packets.OfType<TradeEndSessionPacket>().Count());
        Assert.IsTrue(trades.Invite(b, new Peer(4, this)));
    }

    [TestMethod]
    public void InitialCoinOffersDoNotCloseEitherWindow()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        trades.Invite(a, b);
        var id = Invitation(b);
        trades.Reply(b, id, true);
        trades.OfferCoins(a, id, 0);
        trades.OfferCoins(b, id, 0);
        // Invalid coin offers are clamped to the available wallet without closing the trade.
        trades.OfferCoins(a, id, 100);
        Assert.AreEqual(0, a.Packets.OfType<TradeEndSessionPacket>().Count());
        Assert.AreEqual(0, b.Packets.OfType<TradeEndSessionPacket>().Count());
        Assert.IsFalse(trades.Invite(a, new Peer(3, this)));
        trades.Cancel(b, id);
        Assert.AreEqual(1, a.Packets.OfType<TradeEndSessionPacket>().Count());
        Assert.AreEqual(1, b.Packets.OfType<TradeEndSessionPacket>().Count());
    }

    [TestMethod]
    public void RequestFeedbackEscapesNamesAndIsNotRepeatedForBusyRequests()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        b.TradeName.FirstName = "<Test&Name>";
        Assert.IsTrue(trades.Invite(a, b));
        Assert.IsFalse(trades.Invite(a, b));
        Assert.AreEqual("Sent &lt;Test&amp;Name&gt; a trade request.", a.Packets.OfType<ExecuteScriptWithStringParamsPacket>().Single().Params[0]);
    }

    [TestMethod]
    public void BothCheckmarksStartReviewOnceAndCancelReturnsToEditing()
    {
        using var trades = Create();
        var a = new Peer(1, this); var b = new Peer(2, this);
        trades.Invite(a, b); var id = Invitation(b); trades.Reply(b, id, true);
        a.Packets.Clear(); b.Packets.Clear();
        trades.SetLocked(a, id, 1, true);
        Assert.IsTrue(b.Packets.OfType<TradeUpdateLockPacket>().Single().Locked);
        Assert.AreEqual(0, a.Packets.OfType<TradeConfirmPacket>().Count());
        trades.SetLocked(b, id, 1, true);
        trades.SetLocked(b, id, 1, true);
        Assert.AreEqual(1, a.Packets.OfType<TradeConfirmPacket>().Count());
        Assert.AreEqual(1, b.Packets.OfType<TradeConfirmPacket>().Count());
        trades.SetLocked(a, id, 1, false);
        var count = b.Packets.Count;
        trades.SetLocked(b, id, 1, false); // Native Hide echoes an unlock.
        Assert.AreEqual(count, b.Packets.Count);
        Assert.IsFalse(a.Packets.OfType<TradeUpdateLockPacket>().Last().Locked);
        Assert.AreEqual(0, a.Packets.OfType<TradeEndSessionPacket>().Count());
        trades.SetLocked(a, id, 1, true); trades.SetLocked(b, id, 1, true);
        Assert.AreEqual(2, a.Packets.OfType<TradeConfirmPacket>().Count());
    }

    [TestMethod]
    public void OfferChangeInvalidatesReviewAndStaleOrForgedLocksCannotConfirm()
    {
        using var trades = Create();
        var a = new Peer(1, this); var b = new Peer(2, this);
        Stock(a);
        trades.Invite(a, b); var id = Invitation(b); trades.Reply(b, id, true);
        trades.SetLocked(new Peer(1, this), id, 1, true);
        trades.SetLocked(a, id, 1, true); trades.SetLocked(b, id, 1, true);
        trades.OfferItem(a, id, 7, 1);
        Assert.IsFalse(b.Packets.OfType<TradeUpdateLockPacket>().Last().Locked);
        trades.SetLocked(a, id, 1, true); trades.SetLocked(b, id, 1, true);
        Assert.AreEqual(1, b.Packets.OfType<TradeConfirmPacket>().Count());
        trades.SetLocked(a, id, 2, true); trades.SetLocked(b, id, 2, true);
        Assert.AreEqual(2, b.Packets.OfType<TradeConfirmPacket>().Count());
        Assert.AreEqual(0, b.Packets.OfType<TradeEndSessionPacket>().Count());
    }

    [TestMethod]
    public void FinalConfirmationRequiresBothPlayersAndReviewDelayWithoutSettlement()
    {
        var clock = new Clock();
        using var trades = Create(clock);
        var a = new Peer(1, this); var b = new Peer(2, this);
        Stock(a);
        trades.Invite(a, b); var id = Invitation(b); trades.Reply(b, id, true);
        a.Packets.Clear(); b.Packets.Clear();
        trades.SetLocked(a, id, 1, true); trades.SetLocked(b, id, 1, true);
        trades.Accept(a, id, 1); trades.Accept(b, id, 1);
        Assert.AreEqual(0, b.Packets.OfType<ExecuteScriptWithStringParamsPacket>().Count());
        clock.Now += TradeManager.ReviewDelay;
        trades.Accept(a, id, 0); trades.Accept(new Peer(2, this), id, 1);
        trades.Accept(a, id, 1); trades.Accept(a, id, 1);
        Assert.AreEqual(0, b.Packets.OfType<ExecuteScriptWithStringParamsPacket>().Count());
        trades.Accept(b, id, 1);
        var notice = b.Packets.OfType<ExecuteScriptWithStringParamsPacket>().Single();
        StringAssert.Contains(notice.Params[0], "Trade could not be completed");
        trades.Accept(b, id, 1);
        Assert.AreEqual(1, b.Packets.OfType<ExecuteScriptWithStringParamsPacket>().Count());
        Assert.AreEqual(10, a.Items[7].Item.Count);
        Assert.AreEqual(0, b.Items.Count);
        Assert.AreEqual(1, b.Packets.OfType<TradeEndSessionPacket>().Count());
    }

    private static void Stock(Peer peer, int itemId = 7, int available = 4, bool noTrade = false)
    {
        peer.Items[itemId] = (new ClientItem { Id = itemId, Definition = 100, Tint = 5, Count = 10, ActivateEnabled = true },
            new ClientItemDefinition { NoTrade = noTrade }, available);
    }

    [TestMethod]
    public void ItemOffersAddUpdateAndRemoveOnBothSidesWithoutChangingInventory()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        Stock(a);
        trades.Invite(a, b);
        var id = Invitation(b);
        trades.Reply(b, id, true);
        a.Packets.Clear(); b.Packets.Clear();
        trades.OfferItem(a, id, 7, 1);
        Assert.IsInstanceOfType<PlayerUpdatePacketItemDefinitions>(b.Packets[0]);
        var mine = a.Packets.OfType<TradeUpdateAddItemPacket>().Single();
        var theirs = b.Packets.OfType<TradeUpdateAddItemPacket>().Single();
        Assert.IsTrue(mine.IsMine); Assert.IsFalse(theirs.IsMine);
        Assert.AreEqual(1, theirs.Item.Count);
        Assert.AreEqual(100, theirs.Item.Definition);
        Assert.AreEqual(5, theirs.Item.Tint);
        Assert.AreEqual(2, theirs.Revision);
        trades.OfferItem(a, id, 7, 3);
        trades.OfferItem(a, id, 7, 3); // Retransmitted total must not add another three.
        Assert.AreEqual(3, b.Packets.OfType<TradeUpdateItemCountPacket>().Last().OfferedTotal);
        trades.OfferItem(a, id, 7, 2);
        Assert.AreEqual(2, b.Packets.OfType<TradeUpdateItemCountPacket>().Last().OfferedTotal);
        trades.OfferItem(a, id, 7, 0);
        Assert.AreEqual(0, b.Packets.OfType<TradeUpdateItemCountPacket>().Last().OfferedTotal);
        trades.OfferItem(a, id, 7, 1);
        Assert.AreEqual(2, b.Packets.OfType<TradeUpdateAddItemPacket>().Count());
        Assert.AreEqual(10, a.Items[7].Item.Count);
        Assert.AreEqual(0, a.Packets.OfType<TradeEndSessionPacket>().Count());
        Assert.AreEqual(0, b.Items.Count);
    }

    [TestMethod]
    public void ItemOffersAreScopedToOwnerAndValidateAvailableQuantity()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        Stock(a, available: 4); Stock(b, available: 2);
        trades.Invite(a, b);
        var id = Invitation(b);
        trades.Reply(b, id, true);
        trades.OfferItem(a, id, 7, int.MaxValue);
        trades.OfferItem(b, id, 7, 1);
        var rows = a.Packets.OfType<TradeUpdateAddItemPacket>().ToArray();
        Assert.AreEqual(2, rows.Length);
        Assert.IsTrue(rows[0].IsMine); Assert.AreEqual(4, rows[0].Item.Count);
        Assert.IsFalse(rows[1].IsMine); Assert.AreEqual(1, rows[1].Item.Count);
        Stock(a, noTrade: true);
        trades.OfferItem(a, id, 7, 1);
        Assert.AreEqual(0, b.Packets.OfType<TradeUpdateItemCountPacket>().Last().OfferedTotal);
        trades.OfferItem(b, id, 7, 2);
        Assert.AreEqual(2, b.Packets.OfType<TradeUpdateItemCountPacket>().Last().OfferedTotal);
        Stock(a, itemId: 8, available: 0);
        trades.OfferItem(a, id, 8, 1);
        trades.OfferItem(a, id, 999, 1);
        trades.OfferItem(a, id, 7, -1);
        Assert.AreEqual(2, a.Packets.OfType<TradeUpdateAddItemPacket>().Count());
        Assert.AreEqual(0, a.Packets.OfType<TradeEndSessionPacket>().Count());
    }

    [TestMethod]
    public void ItemOffersRequireActiveSessionAndExactParticipant()
    {
        using var trades = Create();
        var a = new Peer(1, this);
        var b = new Peer(2, this);
        var stranger = new Peer(1, this);
        Stock(a); Stock(stranger);
        trades.Invite(a, b);
        var id = Invitation(b);
        trades.OfferItem(a, id, 7, 1);
        Assert.AreEqual(0, b.Packets.OfType<TradeUpdateAddItemPacket>().Count());
        trades.Reply(b, id, true);
        trades.OfferItem(stranger, id, 7, 1);
        trades.OfferItem(a, id + 1, 7, 1);
        Assert.AreEqual(0, b.Packets.OfType<TradeUpdateAddItemPacket>().Count());
        trades.Cancel(a, id);
        trades.OfferItem(a, id, 7, 1);
        Assert.AreEqual(0, b.Packets.OfType<TradeUpdateAddItemPacket>().Count());
    }

    [TestMethod]
    public void TradeRowsSerializeExactItemInstanceWithoutClientItemExtraByte()
    {
        var add = new TradeUpdateAddItemPacket
        {
            SessionId = 42, Revision = 2, IsMine = true,
            Item = new ClientItem { Id = 7, Definition = 100, Tint = 5, Count = 3, ActivateEnabled = true }
        }.Serialize();
        Assert.AreEqual(41, add.Length);
        Assert.AreEqual("3B000C2A00000002000000016400000005000000070000000300000000000000FFFFFF7F0000000000", Convert.ToHexString(add));
        var count = new TradeUpdateItemCountPacket { SessionId = 42, Revision = 3, IsMine = false, ItemInstanceId = 7, OfferedTotal = 0 }.Serialize();
        Assert.AreEqual("3B000D2A00000003000000000700000000000000", Convert.ToHexString(count));
    }

    [TestMethod]
    public void PacketLayoutsMatchRecoveredWireOrder()
    {
        Assert.AreEqual("3B00112A00000001", Convert.ToHexString(new TradeUpdateLockPacket { SessionId = 42, Locked = true }.Serialize()));
        Assert.AreEqual("3B00122A000000", Convert.ToHexString(new TradeConfirmPacket { SessionId = 42 }.Serialize()));
        var notification = new ExecuteScriptWithStringParamsPacket { Script = "X.Y", Params = ["Hi", "0", "0xFFFF00"] }.Serialize();
        Assert.AreEqual("2F000803000000582E59030000000200000048690100000030080000003078464646463030", Convert.ToHexString(notification));
        var name = new NameData { FirstNameId = 1, LastNamePrefixId = 2, LastNameSuffixId = 3, FirstName = "A", LastName = "B" };
        var invite = new TradeInvitePacket { InviterName = name, InviterGuid = 0x0102030405060708, SessionId = 42 }.Serialize();
        Assert.AreEqual("3B00020100000002000000030000000100000041010000004208070605040302012A000000", Convert.ToHexString(invite));
        var start = new TradeStartSessionPacket { PartnerName = name, PartnerGuid = 0x0102030405060708, SessionId = 42 }.Serialize();
        Assert.AreEqual("3B00040807060504030201010000000200000003000000010000004101000000422A000000", Convert.ToHexString(start));
        Assert.AreEqual("3B00052A000000", Convert.ToHexString(new TradeEndSessionPacket { SessionId = 42 }.Serialize()));
        Assert.AreEqual("3B001001000000020000000300000001000000410100000042", Convert.ToHexString(new TradeRejectedPacket { RejectingPlayer = name }.Serialize()));
    }
}
