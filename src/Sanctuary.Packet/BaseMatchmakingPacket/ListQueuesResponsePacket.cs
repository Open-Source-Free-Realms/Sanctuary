using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ListQueuesResponsePacket : BaseNameChangePacket, ISerializablePacket
{
    public new const short OpCode = 2;

    public ulong Guid;

    public List<MatchmakingQueueDefinition> Queues = [];

    public ListQueuesResponsePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(Queues);

        return writer.Buffer;
    }
}