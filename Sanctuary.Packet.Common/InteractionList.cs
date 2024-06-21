using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class InteractionList
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
}