using Sanctuary.Packet.Common;
using Sanctuary.Core.IO;

namespace Sanctuary.Game;

// Implemented by the authenticated Player instance, not supplied by packet data.
public interface ITradeParticipant
{
    ulong TradeGuid { get; }
    ulong TradeCharacterId { get; }
    int TradeCoins { get; }
    System.Collections.Generic.IReadOnlyList<ISerializablePacket> ApplyTradeResult(TradeParticipantResult result);
    void DisconnectForTradeResync();
    NameData TradeName { get; }
    object TradeContext { get; }
    bool TradeAvailable { get; }
    bool TryGetTradeItem(int itemId, out ItemInstance item, out ClientItemDefinition definition, out int available);
    void SendTunneled(ISerializablePacket packet);
}
