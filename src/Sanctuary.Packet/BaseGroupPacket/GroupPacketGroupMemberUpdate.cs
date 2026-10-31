using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GroupPacketGroupMemberUpdate : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 9;

    public GroupMember Member = new();

    public bool Unknown;

    public GroupPacketGroupMemberUpdate() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Member.Serialize(writer);

        writer.Write(Unknown);

        return writer.Buffer;
    }
}
