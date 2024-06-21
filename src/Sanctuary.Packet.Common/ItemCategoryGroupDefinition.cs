using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ItemCategoryGroupDefinition : ISerializableType
{
    public int Id;
    public int CategoryId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(CategoryId);
    }
}