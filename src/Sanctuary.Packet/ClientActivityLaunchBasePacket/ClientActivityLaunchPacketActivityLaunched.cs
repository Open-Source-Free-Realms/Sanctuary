using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientActivityLaunchPacketActivityLaunched : ClientActivityLaunchBasePacket, ISerializablePacket
{
    public new const int OpCode = 5;

    public List<ulong> Guids = [];

    public ClientActivityLaunchPacketActivityLaunched(int activityId, int unknown) : base(OpCode, activityId, unknown)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guids);

        return writer.Buffer;
    }
}