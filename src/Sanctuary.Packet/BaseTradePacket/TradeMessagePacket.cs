using Sanctuary.Core.IO;
namespace Sanctuary.Packet;
public sealed class TradeMessagePacket : BaseTradePacket
{
    public const byte PartnerDisconnected = 2;
    public const byte Completed = 6;
    // "Your trade with [player] has been canceled." Like 2, it also clears a pending invitation.
    public const byte PartnerEnteredInstancedZone = 8;
    protected override byte SubOpCode => 15;
    public byte MessageCode;
    protected override void WriteBody(PacketWriter writer)
    {
        writer.Write(MessageCode);
    }
}
