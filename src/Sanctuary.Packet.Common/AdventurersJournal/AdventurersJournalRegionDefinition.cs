using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class AdventurersJournalRegionDefinition : ISerializableType
{
    public int Id;

    public int NameId;
    public int DescriptionId;

    public int TabImageId;
    public int ChapterMapImageId;

    public int GeometryId;

    public int CompletedStringId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(TabImageId);
        writer.Write(ChapterMapImageId);
        writer.Write(GeometryId);
        writer.Write(CompletedStringId);
    }
}