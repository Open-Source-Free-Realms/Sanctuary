using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class AdventurersJournalStickerDefinition : ISerializableType
{
    public int Id;

    public int RegionId;
    public int DisplayOrder;

    public int QuestId;

    public int NameId;
    public int DescriptionId;

    public int CompletedImageSetId;
    public int ImageSetId;

    public int Unknown;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(RegionId);
        writer.Write(DisplayOrder);

        writer.Write(QuestId);

        writer.Write(NameId);
        writer.Write(DescriptionId);

        writer.Write(CompletedImageSetId);
        writer.Write(ImageSetId);

        writer.Write(Unknown);
    }
}