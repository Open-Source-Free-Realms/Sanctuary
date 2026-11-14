using System.Collections.Generic;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game;

public sealed class TradeValidationException(string message) : System.InvalidOperationException(message);

// TODO: Share inventory change records/publication with rewards and shop transactions.
public sealed record TradeItem(int Id, int Definition, int Tint, int Count);
// Offer counts are quantities to transfer; result counts are final inventory totals (zero deletes).
public sealed record TradeOffer(ITradeParticipant Participant, IReadOnlyCollection<TradeItem> Items, int Coins);
// A changed stack and the definition validated for it, so publication never looks it up again.
public sealed record TradeItemResult(TradeItem Item, ClientItemDefinition Definition);
public sealed record TradeParticipantResult(IReadOnlyList<TradeItemResult> Items, int Coins);
public sealed record TradeSettlementResult(TradeParticipantResult First, TradeParticipantResult Second);

public interface ITradeSettlement
{
    TradeSettlementResult Commit(TradeOffer first, TradeOffer second);
}
