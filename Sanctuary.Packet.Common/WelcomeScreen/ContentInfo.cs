using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ContentInfo : ISerializableType
{
    public int NameId;
    public int DescriptionId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(NameId);
        writer.Write(DescriptionId);
    }
}