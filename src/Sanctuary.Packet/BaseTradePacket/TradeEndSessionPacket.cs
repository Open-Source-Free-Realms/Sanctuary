using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public sealed class TradeEndSessionPacket : BaseTradePacket
{
    protected override byte SubOpCode => 5;
    public int SessionId;

    protected override void WriteBody(PacketWriter writer)
    {
        writer.Write(SessionId);
    }
}
