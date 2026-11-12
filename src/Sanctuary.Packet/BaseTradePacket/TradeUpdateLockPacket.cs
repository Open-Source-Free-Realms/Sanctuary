using Sanctuary.Core.IO;
namespace Sanctuary.Packet;

public sealed class TradeUpdateLockPacket : BaseTradePacket
{
    protected override byte SubOpCode => 17;
    public int SessionId;
    public bool Locked;
    protected override void WriteBody(PacketWriter writer)
    {
        writer.Write(SessionId);
        writer.Write(Locked);
    }
}
