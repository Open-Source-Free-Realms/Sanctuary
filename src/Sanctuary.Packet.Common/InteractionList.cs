using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class InteractionList : ISerializableType, IDeserializableType
{
    public ulong Guid;

    public bool Unknown;

    public List<InteractionData> Interactions = new();

    public string Name = null!;

    public bool Unknown2;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);

        writer.Write(Unknown);

        writer.Write(Interactions);

        writer.Write(Name);

        writer.Write(Unknown2);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out Guid))
            return false;
        if (!reader.TryRead(out Unknown))
            return false;
        if (!reader.TryReadList(out Interactions))
            return false;
        if (!reader.TryRead(out Name))
            return false;
        return reader.TryRead(out Unknown2);
    }

}