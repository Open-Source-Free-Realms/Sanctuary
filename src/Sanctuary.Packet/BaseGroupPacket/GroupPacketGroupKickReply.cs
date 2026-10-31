using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GroupPacketGroupKickReply : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 7;

    public GroupKickResult Result;

    public GroupPacketGroupKickReply() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write((uint)Result);

        return writer.Buffer;
    }
}
