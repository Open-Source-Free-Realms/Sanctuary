using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GroupPacketGroupUpdate : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 8;

    public List<GroupMember> Members = [];

    public GroupPacketGroupUpdate() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Members);

        return writer.Buffer;
    }
}
