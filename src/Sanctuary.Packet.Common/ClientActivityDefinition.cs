using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientActivityDefinition : ISerializableType
{
    public int Id { get; set; }
    public int DisplayNameId { get; set; }
    public int DisplayDescriptionId { get; set; }
    public int NameId { get; set; }
    public int DescriptionId { get; set; }
    public int Category { get; set; }
    public int ImageSetId { get; set; }
    public int ActivityPositionId { get; set; }
    public int CanPlayerJoin { get; set; } = 1;
    public bool IsFeatured { get; set; }
    public int IsPreferred { get; set; }
    public int FeaturedRewardTooltipStringId { get; set; }
    public int TutorialActivityId { get; set; }
    public bool MembersOnly { get; set; }
    public string DetailImageFilename { get; set; } = string.Empty;
    public string ThumbnailImageFilename { get; set; } = string.Empty;
    public int Difficulty { get; set; }
    public int AppSystemId { get; set; }
    public int MysteryChestIcon { get; set; }
    public int MinigameDetail { get; set; } = 1;
    public int RewardsData { get; set; }

    public Dictionary<int, FeaturedActivityEntry> FeaturedEntries { get; } = [];

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(DisplayNameId);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(Category);
        writer.Write(ActivityPositionId);
        writer.Write(CanPlayerJoin);
        writer.Write(IsPreferred);
        writer.Write(FeaturedRewardTooltipStringId);
        writer.Write(ImageSetId);
        writer.Write(IsFeatured);
        writer.Write(TutorialActivityId);
        writer.Write(FeaturedEntries);
        writer.Write(AppSystemId);
        writer.Write(MembersOnly);
        writer.Write(DetailImageFilename);
        writer.Write(ThumbnailImageFilename);
        writer.Write(Difficulty);
        writer.Write(DisplayDescriptionId);
        writer.Write(MinigameDetail);
        writer.Write(RewardsData);
    }

    public class FeaturedActivityEntry : ISerializableType
    {
        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }
        public int Unknown3 { get; set; }
        public int Unknown4 { get; set; }
        public int Unknown5 { get; set; }
        public bool Unknown6 { get; set; }
        public int Unknown7 { get; set; }
        public int Unknown8 { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Unknown1);
            writer.Write(Unknown2);
            writer.Write(Unknown3);
            writer.Write(Unknown4);
            writer.Write(Unknown5);
            writer.Write(Unknown6);
            writer.Write(Unknown7);
            writer.Write(Unknown8);
        }
    }
}
