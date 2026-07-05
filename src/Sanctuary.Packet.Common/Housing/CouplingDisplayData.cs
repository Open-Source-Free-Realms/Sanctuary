using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class CouplingDisplayData : ISerializableType
{
    public int Id;

    public List<ulong> NpcGuids = [];

    public int CompositeEffect;

    // 1 - ?
    // 2 - ?
    // 3 - ?
    public int EffectType;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(NpcGuids);

        writer.Write(CompositeEffect);
        writer.Write(EffectType);
    }
}