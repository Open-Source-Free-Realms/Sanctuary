using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Sanctuary.Core.IO;
using Sanctuary.Game.Helpers;
using Microsoft.Extensions.Logging;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game;

public sealed class TradeManager : IDisposable
{
    private sealed class TradeSide(ITradeParticipant player)
    {
        public readonly ITradeParticipant Player = player;
        public readonly Dictionary<int, TradeItem> Items = [];
        public int Coins;
        public bool Locked;
        public bool Accepted;
    }

    private sealed class Session(int id, ITradeParticipant inviter, ITradeParticipant target, DateTimeOffset expires)
    {
        public readonly int Id = id;
        public readonly TradeSide Inviter = new(inviter);
        public readonly TradeSide Target = new(target);
        public readonly object Context = inviter.TradeContext;
        public readonly DateTimeOffset Expires = expires;
        public bool Active;
        public int Revision = 1;
        public bool Completing;
        public bool Contains(ITradeParticipant player) => ReferenceEquals(player, Inviter.Player) || ReferenceEquals(player, Target.Player);
        public TradeSide SideOf(ITradeParticipant player) => ReferenceEquals(player, Inviter.Player) ? Inviter : Target;
        public ITradeParticipant PartnerOf(ITradeParticipant player) => ReferenceEquals(player, Inviter.Player) ? Target.Player : Inviter.Player;
    }

    // Serializes trade session transitions, including duplicate final accepts.
    // Other gameplay inventory changes are not synchronized by this lock.
    private readonly object _gate = new();
    private readonly ITradeSettlement? _settlement;
    private readonly Dictionary<int, Session> _sessions = [];
    private readonly Dictionary<ulong, Session> _players = [];
    private readonly ILogger<TradeManager> _logger;
    private readonly TimeProvider _clock;
    private int _nextId;
    private bool _disposed;
    // Restoration policy, not a recovered retail timeout.
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromSeconds(60);

    public TradeManager(ILogger<TradeManager> logger, TimeProvider? clock = null, ITradeSettlement? settlement = null)
    {
        _logger = logger;
        _settlement = settlement;
        _clock = clock ?? TimeProvider.System;
    }

    public bool Invite(ITradeParticipant inviter, ITradeParticipant target)
    {
        lock (_gate)
        {
            Prune(inviter);
            Prune(target);
            if (_disposed || inviter.TradeGuid == target.TradeGuid || !inviter.TradeAvailable || !target.TradeAvailable
                || !ReferenceEquals(inviter.TradeContext, target.TradeContext)
                || _players.ContainsKey(inviter.TradeGuid) || _players.ContainsKey(target.TradeGuid)
                || _nextId == int.MaxValue)
                return false;

            var session = new Session(++_nextId, inviter, target, _clock.GetUtcNow() + InvitationLifetime);
            _sessions.Add(session.Id, session);
            _players.Add(inviter.TradeGuid, session);
            _players.Add(target.TradeGuid, session);
            if (!Send(target, new TradeInvitePacket { InviterName = inviter.TradeName, InviterGuid = inviter.TradeGuid, SessionId = session.Id }))
            {
                Close(session);
                return false;
            }
            _logger.LogInformation("Trade {session}: invitation from {inviter} to {target}.", session.Id, inviter.TradeGuid, target.TradeGuid);
            SendSystemMessage(inviter, $"Sent {target.TradeName.FullName} a trade request.");
            return true;
        }
    }

    public void Reply(ITradeParticipant player, int id, bool accepted)
    {
        lock (_gate)
        {
            if (!TryGetSession(player, id, out var session) || session.Active || !ReferenceEquals(player, session.Target.Player))
                return;
            if (!accepted)
            {
                // Native TradeRejected substitutes the player's name and shows a yellow SystemMessage.
                Send(session.Inviter.Player, new TradeRejectedPacket { RejectingPlayer = player.TradeName });
                Close(session);
                return;
            }
            session.Active = true;
            if (!Send(session.Inviter.Player, new TradeStartSessionPacket { PartnerGuid = session.Target.Player.TradeGuid, PartnerName = session.Target.Player.TradeName, SessionId = id })
                || !Send(session.Target.Player, new TradeStartSessionPacket { PartnerGuid = session.Inviter.Player.TradeGuid, PartnerName = session.Inviter.Player.TradeName, SessionId = id }))
            {
                Close(session);
                return;
            }
            _logger.LogInformation("Trade {session}: both windows started.", id);
        }
    }

    public void SetLocked(ITradeParticipant player, int id, int revision, bool locked)
    {
        lock (_gate)
        {
            if (!TryGetSession(player, id, out var session) || !session.Active || !session.Contains(player))
                return;
            if (!locked)
            {
                ResetReview(session);
                return;
            }
            if (revision != session.Revision)
            {
                ResetReview(session);
                Send(player, new TradeUpdateLockPacket { SessionId = id, Locked = false });
                return;
            }
            var side = session.SideOf(player);
            if (side.Locked)
                return;
            side.Locked = true;
            if (!Send(session.PartnerOf(player), new TradeUpdateLockPacket { SessionId = id, Locked = true }))
            {
                Close(session);
                return;
            }
            if (session.Inviter.Locked && session.Target.Locked)
            {
                var confirm = new TradeConfirmPacket { SessionId = id };
                if (!Broadcast(session, confirm))
                    Close(session);
                else
                    _logger.LogInformation("Trade {session}: both offers locked at revision {revision}; review started.", id, revision);
            }
        }
    }

    private void ResetReview(Session session)
    {
        var changed = session.Inviter.Locked || session.Target.Locked;
        session.Inviter.Locked = session.Target.Locked = false;
        session.Inviter.Accepted = session.Target.Accepted = false;
        // Hiding the client's review dialog echoes CancelLock. Do not echo it forever.
        if (!changed)
            return;
        var unlock = new TradeUpdateLockPacket { SessionId = session.Id, Locked = false };
        Broadcast(session, unlock);
    }

    public void Accept(ITradeParticipant player, int id, int revision)
    {
        lock (_gate)
        {
            if (!TryGetSession(player, id, out var session) || !session.Active || !session.Contains(player)
                || session.Completing || revision != session.Revision || !session.Inviter.Locked || !session.Target.Locked)
                return;
            // The native confirmation UI owns its countdown; never silently discard an accept.
            session.SideOf(player).Accepted = true;
            if (!session.Inviter.Accepted || !session.Target.Accepted)
                return;

            session.Completing = true;
            TradeSettlementResult result;
            try
            {
                if (_settlement is null)
                    throw new TradeValidationException("Trade settlement service is unavailable.");
                result = _settlement.Commit(
                    new(session.Inviter.Player, session.Inviter.Items.Values.ToArray(), session.Inviter.Coins),
                    new(session.Target.Player, session.Target.Items.Values.ToArray(), session.Target.Coins));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trade {session}: exchange failed; session will not be replayed.", id);
                Close(session);
                Broadcast(session, SystemMessage("Trade could not be completed. Please check your inventory before trying again."));
                // An unexpected database/connection error may leave commit outcome uncertain.
                // Force a durable reload rather than letting either player spend stale inventory.
                if (ex is not TradeValidationException)
                {
                    session.Inviter.Player.DisconnectForTradeResync();
                    session.Target.Player.DisconnectForTradeResync();
                }
                return;
            }
            // Durable commit is the point of no return. Forget the session before notifications,
            // so duplicate accepts or a failed network send can never run the exchange again.
            _sessions.Remove(id);
            _players.Remove(session.Inviter.Player.TradeGuid);
            _players.Remove(session.Target.Player.TradeGuid);
            PublishCommitted(session.Inviter.Player, result.First, id);
            PublishCommitted(session.Target.Player, result.Second, id);
            _logger.LogInformation("Trade {session}: database exchange committed.", id);
        }
    }

    private void PublishCommitted(ITradeParticipant player, TradeParticipantResult result, int id)
    {
        try
        {
            var packets = player.ApplyTradeResult(result);
            foreach (var packet in packets)
                if (!Send(player, packet))
                    throw new InvalidOperationException("Committed inventory could not be delivered.");
            if (!Send(player, new TradeMessagePacket { MessageCode = TradeMessagePacket.Completed })
                || !Send(player, new TradeEndSessionPacket { SessionId = id }))
                throw new InvalidOperationException("Trade completion could not be delivered.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trade {session}: committed, but player {player} needs an inventory reload.", id, player.TradeGuid);
            player.DisconnectForTradeResync();
        }
    }

    public void OfferItem(ITradeParticipant player, int id, int itemId, int requestedTotal)
    {
        lock (_gate)
        {
            if (!TryGetSession(player, id, out var session) || !session.Active || !session.Contains(player))
                return;
            var partner = session.PartnerOf(player);
            var offers = session.SideOf(player).Items;
            offers.TryGetValue(itemId, out var priorOffer);
            var previous = priorOffer?.Count ?? 0;
            if (requestedTotal < 0)
            {
                SendSystemMessage(player, "That item quantity is invalid.");
                return;
            }
            var total = 0;
            ItemInstance item = null!;
            ClientItemDefinition definition = null!;
            if (requestedTotal > 0)
            {
                if (!player.TryGetTradeItem(itemId, out item, out definition, out var available)
                    || item.Id != itemId || !TradeItemRules.CanTrade(item, definition) || available <= 0)
                {
                    SendSystemMessage(player, "That item is unavailable or cannot be traded.");
                    return;
                }
                total = Math.Min(requestedTotal, Math.Max(0, available));
            }
            if (total == previous && (total == 0 || priorOffer!.Definition == item.Definition && priorOffer.Tint == item.Tint))
            {
                if (requestedTotal != total && priorOffer is not null)
                    Send(player, new TradeUpdateItemCountPacket
                    {
                        SessionId = id, Revision = session.Revision, IsMine = true,
                        ItemInstanceId = itemId, OfferedTotal = total
                    });
                return;
            }

            if (session.Revision == int.MaxValue)
            {
                Close(session);
                return;
            }
            ResetReview(session);
            // C2S quantity is the absolute offered total, never an increment.
            var revision = ++session.Revision;
            if (total > 0)
                offers[itemId] = new(itemId, item.Definition, item.Tint, total);
            else
                offers.Remove(itemId);

            foreach (var recipient in new[] { player, partner })
            {
                bool sent;
                if (previous == 0 && total > 0)
                {
                    // The other client may never have encountered this definition.
                    using var writer = new PacketWriter();
                    writer.Write(1);
                    definition.Serialize(writer);
                    sent = Send(recipient, new PlayerUpdatePacketItemDefinitions { Payload = writer.Buffer })
                        && Send(recipient, new TradeUpdateAddItemPacket
                        {
                            SessionId = id, Revision = revision, IsMine = ReferenceEquals(recipient, player),
                            Item = new ItemInstance
                            {
                                Definition = item.Definition, Tint = item.Tint, Id = item.Id, Count = total,
                                ConsumedCount = 0, LastCastTime = item.LastCastTime, AbilityCount = item.AbilityCount
                            }
                        });
                }
                else
                    sent = Send(recipient, new TradeUpdateItemCountPacket
                    {
                        SessionId = id, Revision = revision, IsMine = ReferenceEquals(recipient, player),
                        ItemInstanceId = itemId, OfferedTotal = total
                    });
                if (!sent)
                {
                    Close(session);
                    return;
                }
            }
            _logger.LogInformation("Trade {session}: player {player} offers {count} of item {item}, revision {revision}.", id, player.TradeGuid, total, itemId, revision);
        }
    }

    public void OfferCoins(ITradeParticipant player, int id, int coins)
    {
        lock (_gate)
        {
            if (!TryGetSession(player, id, out var session) || !session.Active || !session.Contains(player))
                return;
            var total = Math.Clamp(coins, 0, Math.Max(0, player.TradeCoins));
            var side = session.SideOf(player);
            var previous = side.Coins;
            if (previous == total)
            {
                if (coins != total)
                {
                    SendSystemMessage(player, "That coin amount is unavailable.");
                    Send(player, new TradeUpdateCoinCountPacket
                    { SessionId = id, Revision = session.Revision, IsMine = true, OfferedCoins = total });
                }
                return; // Includes the automatic zero offer when the UI opens.
            }
            ResetReview(session);
            if (session.Revision == int.MaxValue) { Close(session); return; }
            var revision = ++session.Revision;
            side.Coins = total;
            foreach (var recipient in new[] { session.Inviter.Player, session.Target.Player })
                if (!Send(recipient, new TradeUpdateCoinCountPacket
                {
                    SessionId = id, Revision = revision, IsMine = ReferenceEquals(recipient, player), OfferedCoins = total
                })) { Close(session); return; }
        }
    }

    public void Cancel(ITradeParticipant player, int id, bool notifyPartner = true)
    {
        lock (_gate)
            if (TryGetSession(player, id, out var session) && session.Contains(player))
            {
                Close(session);
                if (notifyPartner)
                {
                    var partner = session.PartnerOf(player);
                    SendSystemMessage(partner, $"{player.TradeName.FullName} has cancelled the trade.");
                }
            }
    }

    public void Disconnect(ITradeParticipant player)
    {
        lock (_gate)
            if (_players.TryGetValue(player.TradeGuid, out var session) && session.Contains(player))
                CloseDisconnected(session, player);
    }

    private void CloseDisconnected(Session session, ITradeParticipant disconnected)
    {
        var partner = session.PartnerOf(disconnected);
        // Send before EndSession: native message 2 also clears a pending invitation.
        Send(partner, new TradeMessagePacket { MessageCode = TradeMessagePacket.PartnerDisconnected });
        Close(session);
    }

    // Called by each zone's periodic routine, never by a separate trade timer.
    public void Sweep(object context)
    {
        // Defer only trade maintenance if settlement is busy; don't block the zone tick.
        if (!Monitor.TryEnter(_gate))
            return;
        try
        {
            foreach (var session in _sessions.Values.Where(s => ReferenceEquals(s.Context, context)).ToArray())
                ValidateSession(session);
        }
        finally { Monitor.Exit(_gate); }
    }

    private void Prune(ITradeParticipant player)
    {
        if (_players.TryGetValue(player.TradeGuid, out var session))
            ValidateSession(session);
    }

    private bool TryGetSession(ITradeParticipant player, int id, out Session session)
    {
        if (!_sessions.TryGetValue(id, out session!) || !session.Contains(player))
            return false;
        return ValidateSession(session);
    }

    private bool ValidateSession(Session session)
    {
        if (!session.Inviter.Player.TradeAvailable || !session.Target.Player.TradeAvailable)
        {
            CloseDisconnected(session, !session.Inviter.Player.TradeAvailable ? session.Inviter.Player : session.Target.Player);
            return false;
        }
        if ((!session.Active && _clock.GetUtcNow() >= session.Expires)
            || !ReferenceEquals(session.Context, session.Inviter.Player.TradeContext)
            || !ReferenceEquals(session.Context, session.Target.Player.TradeContext))
        {
            Close(session);
            return false;
        }
        return true;
    }

    private void Close(Session session)
    {
        _sessions.Remove(session.Id);
        _players.Remove(session.Inviter.Player.TradeGuid);
        _players.Remove(session.Target.Player.TradeGuid);
        var end = new TradeEndSessionPacket { SessionId = session.Id };
        Broadcast(session, end);
        _logger.LogInformation("Trade {session}: closed.", session.Id);
    }

    private bool Broadcast(Session session, ISerializablePacket packet)
    {
        var first = Send(session.Inviter.Player, packet);
        var second = Send(session.Target.Player, packet);
        return first && second;
    }

    private bool SendSystemMessage(ITradeParticipant player, string text) => Send(player, SystemMessage(text));

    private static ExecuteScriptWithStringParamsPacket SystemMessage(string text) => new()
    {
        Script = "NotificationHandler.SystemMessage",
        Params = [text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"), "0", "0xFFFF00"]
    };

    private bool Send(ITradeParticipant player, ISerializablePacket packet)
    {
        if (!player.TradeAvailable)
            return false;
        try
        {
            player.SendTunneled(packet);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send trade packet to {player}.", player.TradeGuid);
            return false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var session in _sessions.Values.ToArray())
                Close(session);
        }
    }
}
