using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public sealed class TradeUpdateItemCountPacket : BaseTradePacket
{
    protected override byte SubOpCode => 13;
    public int SessionId;
    public int Revision;
    public bool IsMine;
    public int ItemInstanceId;
    public int OfferedTotal;

    protected override void WriteBody(PacketWriter writer)
    {
        writer.Write(SessionId);
        writer.Write(Revision);
        writer.Write(IsMine);
        writer.Write(ItemInstanceId);
        writer.Write(OfferedTotal);
    }
}
