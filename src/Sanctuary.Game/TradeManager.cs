using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Sanctuary.Core.IO;
using Microsoft.Extensions.Logging;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game;

public sealed class TradeManager : IDisposable
{
    private sealed class Session(int id, ITradeParticipant inviter, ITradeParticipant target, DateTimeOffset expires)
    {
        public readonly int Id = id;
        public readonly ITradeParticipant Inviter = inviter;
        public readonly ITradeParticipant Target = target;
        public readonly object Context = inviter.TradeContext;
        public readonly DateTimeOffset Expires = expires;
        public bool Active;
        public int Revision = 1;
        public bool InviterLocked;
        public bool TargetLocked;
        public bool InviterAccepted;
        public bool TargetAccepted;
        public DateTimeOffset? ReviewReadyAt;
        public readonly Dictionary<int, TradeItemOffer> InviterItems = [];
        public readonly Dictionary<int, TradeItemOffer> TargetItems = [];
        public int InviterCoins;
        public int TargetCoins;
        public bool Completing;
        public bool Contains(ITradeParticipant player) => ReferenceEquals(player, Inviter) || ReferenceEquals(player, Target);
    }

    private readonly object _gate = InventorySynchronization.Gate;
    private readonly ITradeSettlement? _settlement;
    private readonly Dictionary<int, Session> _sessions = [];
    private readonly Dictionary<ulong, Session> _players = [];
    private readonly ILogger<TradeManager> _logger;
    private readonly TimeProvider _clock;
    private readonly ITimer _timer;
    private int _nextId;
    private bool _disposed;
    // Restoration policy, not a recovered retail timeout.
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ReviewDelay = TimeSpan.FromSeconds(10);

    public TradeManager(ILogger<TradeManager> logger, TimeProvider? clock = null, ITradeSettlement? settlement = null)
    {
        _logger = logger;
        _settlement = settlement;
        _clock = clock ?? TimeProvider.System;
        _timer = _clock.CreateTimer(_ => Sweep(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public bool Invite(ITradeParticipant inviter, ITradeParticipant target)
    {
        lock (_gate)
        {
            Sweep();
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
            var targetName = target.TradeName.FullName.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            Send(inviter, new ExecuteScriptWithStringParamsPacket
            {
                Script = "NotificationHandler.SystemMessage",
                Params = [$"Sent {targetName} a trade request.", "0", "0xFFFF00"]
            });
            return true;
        }
    }

    public void Reply(ITradeParticipant player, int id, bool accepted)
    {
        lock (_gate)
        {
            Sweep();
            if (!_sessions.TryGetValue(id, out var session) || session.Active || !ReferenceEquals(player, session.Target))
                return;
            if (!accepted)
            {
                // Native TradeRejected substitutes the player's name and shows a yellow SystemMessage.
                Send(session.Inviter, new TradeRejectedPacket { RejectingPlayer = player.TradeName });
                Close(session);
                return;
            }
            session.Active = true;
            if (!Send(session.Inviter, new TradeStartSessionPacket { PartnerGuid = session.Target.TradeGuid, PartnerName = session.Target.TradeName, SessionId = id })
                || !Send(session.Target, new TradeStartSessionPacket { PartnerGuid = session.Inviter.TradeGuid, PartnerName = session.Inviter.TradeName, SessionId = id }))
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
            Sweep();
            if (!_sessions.TryGetValue(id, out var session) || !session.Active || !session.Contains(player))
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
            var inviter = ReferenceEquals(player, session.Inviter);
            if (inviter ? session.InviterLocked : session.TargetLocked)
                return;
            if (inviter) session.InviterLocked = true;
            else session.TargetLocked = true;
            if (!Send(inviter ? session.Target : session.Inviter, new TradeUpdateLockPacket { SessionId = id, Locked = true }))
            {
                Close(session);
                return;
            }
            if (session.InviterLocked && session.TargetLocked)
            {
                session.ReviewReadyAt = _clock.GetUtcNow() + ReviewDelay;
                var confirm = new TradeConfirmPacket { SessionId = id };
                if (!Send(session.Inviter, confirm) || !Send(session.Target, confirm))
                    Close(session);
                else
                    _logger.LogInformation("Trade {session}: both offers locked at revision {revision}; review started.", id, revision);
            }
        }
    }

    private void ResetReview(Session session)
    {
        var changed = session.InviterLocked || session.TargetLocked || session.ReviewReadyAt.HasValue;
        session.InviterLocked = session.TargetLocked = false;
        session.InviterAccepted = session.TargetAccepted = false;
        session.ReviewReadyAt = null;
        // Hiding the client's review dialog echoes CancelLock. Do not echo it forever.
        if (!changed)
            return;
        var unlock = new TradeUpdateLockPacket { SessionId = session.Id, Locked = false };
        Send(session.Inviter, unlock);
        Send(session.Target, unlock);
    }

    public void Accept(ITradeParticipant player, int id, int revision)
    {
        lock (_gate)
        {
            Sweep();
            if (!_sessions.TryGetValue(id, out var session) || !session.Active || !session.Contains(player)
                || session.Completing || revision != session.Revision || !session.InviterLocked || !session.TargetLocked
                || session.ReviewReadyAt is not { } ready || _clock.GetUtcNow() < ready)
                return;
            if (ReferenceEquals(player, session.Inviter)) session.InviterAccepted = true;
            else session.TargetAccepted = true;
            if (!session.InviterAccepted || !session.TargetAccepted)
                return;

            session.Completing = true;
            TradeSettlementResult result;
            try
            {
                if (_settlement is null)
                    throw new TradeValidationException("Trade settlement service is unavailable.");
                result = _settlement.Commit(session.Inviter, session.Target,
                    session.InviterItems.Values.ToArray(), session.TargetItems.Values.ToArray(),
                    session.InviterCoins, session.TargetCoins);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trade {session}: exchange failed; session will not be replayed.", id);
                Close(session);
                var failure = new ExecuteScriptWithStringParamsPacket
                {
                    Script = "NotificationHandler.SystemMessage",
                    Params = ["Trade could not be completed. Please check your inventory before trying again.", "0", "0xFFFF00"]
                };
                Send(session.Inviter, failure); Send(session.Target, failure);
                // An unexpected database/connection error may leave commit outcome uncertain.
                // Force a durable reload rather than letting either player spend stale inventory.
                if (ex is not TradeValidationException)
                {
                    session.Inviter.DisconnectForTradeResync();
                    session.Target.DisconnectForTradeResync();
                }
                return;
            }
            // Durable commit is the point of no return. Forget the session before notifications,
            // so duplicate accepts or a failed network send can never run the exchange again.
            _sessions.Remove(id);
            _players.Remove(session.Inviter.TradeGuid);
            _players.Remove(session.Target.TradeGuid);
            PublishCommitted(session.Inviter, result.First, id);
            PublishCommitted(session.Target, result.Second, id);
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
            if (!Send(player, new TradeMessagePacket { MessageCode = 6 })
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
            Sweep();
            if (!_sessions.TryGetValue(id, out var session) || !session.Active || !session.Contains(player))
                return;
            var isInviter = ReferenceEquals(player, session.Inviter);
            var partner = isInviter ? session.Target : session.Inviter;
            var offers = isInviter ? session.InviterItems : session.TargetItems;
            offers.TryGetValue(itemId, out var priorOffer);
            var previous = priorOffer?.Quantity ?? 0;
            var total = 0;
            ItemInstance item = null!;
            ClientItemDefinition definition = null!;
            if (requestedTotal > 0 && player.TryGetTradeItem(itemId, out item, out definition, out var available)
                && item.Id == itemId && !definition.NoTrade && TradeSettlement.SupportsItemType(definition.Type) && item.AbilityCount == 0)
                total = Math.Min(requestedTotal, Math.Max(0, available));

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
            Sweep();
            if (!_sessions.TryGetValue(id, out var session) || !session.Active || !session.Contains(player))
                return;
            var total = Math.Clamp(coins, 0, Math.Max(0, player.TradeCoins));
            var inviter = ReferenceEquals(player, session.Inviter);
            var previous = inviter ? session.InviterCoins : session.TargetCoins;
            if (previous == total && coins == total)
                return; // Includes the automatic zero offer when the UI opens.
            ResetReview(session);
            if (session.Revision == int.MaxValue) { Close(session); return; }
            var revision = ++session.Revision;
            if (inviter) session.InviterCoins = total;
            else session.TargetCoins = total;
            foreach (var recipient in new[] { session.Inviter, session.Target })
                if (!Send(recipient, new TradeUpdateCoinCountPacket
                {
                    SessionId = id, Revision = revision, IsMine = ReferenceEquals(recipient, player), OfferedCoins = total
                })) { Close(session); return; }
        }
    }

    public void Cancel(ITradeParticipant player, int id, bool notifyPartner = true)
    {
        lock (_gate)
            if (_sessions.TryGetValue(id, out var session) && session.Contains(player))
            {
                Close(session);
                if (notifyPartner)
                {
                    var partner = ReferenceEquals(player, session.Inviter) ? session.Target : session.Inviter;
                    var name = player.TradeName.FullName.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                    Send(partner, new ExecuteScriptWithStringParamsPacket
                    {
                        Script = "NotificationHandler.SystemMessage",
                        Params = [$"{name} has cancelled the trade.", "0", "0xFFFF00"]
                    });
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
        var partner = ReferenceEquals(disconnected, session.Inviter) ? session.Target : session.Inviter;
        // Send before EndSession: native message 2 also clears a pending invitation.
        Send(partner, new TradeMessagePacket { MessageCode = 2 });
        Close(session);
    }

    public void Sweep()
    {
        lock (_gate)
        {
            foreach (var session in _sessions.Values.ToArray())
                if (!session.Inviter.TradeAvailable || !session.Target.TradeAvailable)
                    CloseDisconnected(session, !session.Inviter.TradeAvailable ? session.Inviter : session.Target);
                else if ((!session.Active && _clock.GetUtcNow() >= session.Expires)
                    || !ReferenceEquals(session.Context, session.Inviter.TradeContext)
                    || !ReferenceEquals(session.Context, session.Target.TradeContext))
                    Close(session);
        }
    }

    private void Close(Session session)
    {
        _sessions.Remove(session.Id);
        _players.Remove(session.Inviter.TradeGuid);
        _players.Remove(session.Target.TradeGuid);
        var end = new TradeEndSessionPacket { SessionId = session.Id };
        Send(session.Inviter, end);
        Send(session.Target, end);
        _logger.LogInformation("Trade {session}: closed.", session.Id);
    }

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
            _timer.Dispose();
            foreach (var session in _sessions.Values.ToArray())
                Close(session);
        }
    }
}
