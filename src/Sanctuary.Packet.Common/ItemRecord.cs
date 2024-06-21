using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ItemRecord : ISerializableType
{
    public int Definition;
    public int Tint;

    public virtual void Serialize(PacketWriter writer)
    {
        writer.Write(Definition);
        writer.Write(Tint);
    }
}