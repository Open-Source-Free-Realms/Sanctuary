using Sanctuary.Core.IO;
namespace Sanctuary.Packet;
public sealed class TradeMessagePacket : BaseTradePacket
{
    public const byte PartnerDisconnected = 2;
    public const byte Completed = 6;
    protected override byte SubOpCode => 15;
    public byte MessageCode;
    protected override void WriteBody(PacketWriter writer)
    {
        writer.Write(MessageCode);
    }
}
