using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public sealed class TradeInvitePacket : BaseTradePacket
{
    protected override byte SubOpCode => 2;
    public NameData InviterName = new();
    public ulong InviterGuid;
    public int SessionId;

    protected override void WriteBody(PacketWriter writer)
    {
        InviterName.Serialize(writer);
        writer.Write(InviterGuid);
        writer.Write(SessionId);
    }
}
