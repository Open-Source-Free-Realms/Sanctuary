using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class QuickChatSendChatPacketBase : BaseQuickChatPacket
{
    public int Id;

    public ulong Guid;

    public NameData Name = new();

    public QuickChatSendChatPacketBase(short subOpCode) : base(subOpCode)
    {
    }

    public override void Write(PacketWriter writer)
    {
        base.Write(writer);

        writer.Write(Id);

        writer.Write(Guid);

        Name.Serialize(writer);
    }

    public override bool TryRead(ref PacketReader reader)
    {
        if (!base.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out Id))
            return false;

        if (!reader.TryRead(out Guid))
            return false;

        if (!Name.TryRead(ref reader))
            return false;

        return true;
    }
}