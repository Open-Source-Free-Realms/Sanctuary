using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public sealed class TradeRejectedPacket : BaseTradePacket
{
    protected override byte SubOpCode => 16;
    public NameData RejectingPlayer = new();

    protected override void WriteBody(PacketWriter writer)
    {
        RejectingPlayer.Serialize(writer);
    }
}
