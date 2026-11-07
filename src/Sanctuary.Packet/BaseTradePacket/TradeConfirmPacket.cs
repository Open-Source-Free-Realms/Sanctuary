using Sanctuary.Core.IO;
namespace Sanctuary.Packet;

public sealed class TradeConfirmPacket : BaseTradePacket
{
    protected override byte SubOpCode => 18;
    public int SessionId;
    
    protected override void WriteBody(PacketWriter writer)
    {
        writer.Write(SessionId);
        
    }
}
