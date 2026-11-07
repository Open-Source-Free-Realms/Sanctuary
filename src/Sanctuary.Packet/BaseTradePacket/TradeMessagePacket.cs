using Sanctuary.Core.IO;
namespace Sanctuary.Packet;
public sealed class TradeMessagePacket : BaseTradePacket
{
    protected override byte SubOpCode => 15;
    public byte MessageCode;
    protected override void WriteBody(PacketWriter writer)
    {
        writer.Write(MessageCode);
    }
}
