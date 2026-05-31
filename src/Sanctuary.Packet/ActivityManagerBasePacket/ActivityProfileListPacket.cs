using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ActivityProfileListPacket : ActivityManagerBasePacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    public Dictionary<int, ActivityForProfileType> Activities = [];

    public ActivityProfileListPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Activities);

        return writer.Buffer;
    }
}