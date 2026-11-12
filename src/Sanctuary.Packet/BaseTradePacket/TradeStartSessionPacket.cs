using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public sealed class TradeStartSessionPacket : BaseTradePacket
{
    protected override byte SubOpCode => 4;
    public ulong PartnerGuid;
    public NameData PartnerName = new();
    public int SessionId;

    protected override void WriteBody(PacketWriter writer)
    {
        writer.Write(PartnerGuid);
        PartnerName.Serialize(writer);
        writer.Write(SessionId);
    }
}
