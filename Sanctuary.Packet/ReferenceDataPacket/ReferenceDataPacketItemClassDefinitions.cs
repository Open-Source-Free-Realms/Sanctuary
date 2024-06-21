using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ReferenceDataPacketItemClassDefinitions : ReferenceDataPacket, ISerializablePacket
{
    public new const short OpCode = 1;

    public Dictionary<int, ItemClassDefinition> ItemClasses = new();

    public ReferenceDataPacketItemClassDefinitions() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ItemClasses);

        return writer.Buffer;
    }
}