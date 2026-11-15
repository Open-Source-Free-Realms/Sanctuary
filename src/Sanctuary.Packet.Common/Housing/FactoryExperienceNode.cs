using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class FactoryExperienceNode : ISerializableType, IDeserializableType
{
    public string? Name;

    public int Unknown2;
    public int Unknown3;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Name);

        writer.Write(Unknown2);
        writer.Write(Unknown3);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out Name))
            return false;

        if (!reader.TryRead(out Unknown2))
            return false;

        if (!reader.TryRead(out Unknown3))
            return false;

        return true;
    }
}
