using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class AdventurersJournalHubDefinition : ISerializableType
{
    public int Id;

    public int RegionId;
    public int DisplayOrder;

    public int NameId;

    public int ActiveImageSetId;
    public int ImageSetId;

    public int CompletedImageSetId;
    public int CompletedDescriptionId;

    public int MapX;
    public int MapY;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(RegionId);
        writer.Write(DisplayOrder);

        writer.Write(NameId);

        writer.Write(ActiveImageSetId);
        writer.Write(ImageSetId);

        writer.Write(CompletedImageSetId);
        writer.Write(CompletedDescriptionId);

        writer.Write(MapX);
        writer.Write(MapY);
    }
}