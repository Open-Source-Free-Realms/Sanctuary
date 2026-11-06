using System.Collections.Generic;

namespace Sanctuary.Game;

public sealed class TradeValidationException(string message) : System.InvalidOperationException(message);

public sealed record TradeItemOffer(int ItemId, int Definition, int Tint, int Quantity);
public sealed record TradeInventoryChange(int Id, int Definition, int Tint, int Count);
public sealed record TradeParticipantResult(IReadOnlyList<TradeInventoryChange> Items, int Coins);
public sealed record TradeSettlementResult(TradeParticipantResult First, TradeParticipantResult Second);

public interface ITradeSettlement
{
    TradeSettlementResult Commit(ITradeParticipant first, ITradeParticipant second,
        IReadOnlyCollection<TradeItemOffer> firstItems, IReadOnlyCollection<TradeItemOffer> secondItems,
        int firstCoins, int secondCoins);
}
