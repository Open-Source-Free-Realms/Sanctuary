using Sanctuary.Core.IO;
namespace Sanctuary.Packet;
public sealed class TradeUpdateCoinCountPacket : BaseTradePacket
{
    protected override byte SubOpCode => 11;
    public int SessionId;
    public int Revision;
    public bool IsMine;
    public int OfferedCoins;
    protected override void WriteBody(PacketWriter writer)
    {
        writer.Write(SessionId);
        writer.Write(Revision);
        writer.Write(IsMine);
        writer.Write(OfferedCoins);
    }
}
