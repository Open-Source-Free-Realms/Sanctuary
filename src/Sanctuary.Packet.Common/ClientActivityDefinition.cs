using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientActivityDefinition : ISerializableType
{
    public int Id { get; set; }

    public int AppSystemId { get; set; }

    public int Unknown3 { get; set; }

    public int Category { get; set; }

    public int ImageSetId { get; set; }
    public int ImagePositionId { get; set; }

    // 1 - PacketTunneledClientWorldPacket
    // 2 - PacketTunneledClientPacket
    public int ServerType { get; set; }

    public int DisplayNameId { get; set; }
    public int DisplayDescriptionId { get; set; }

    public int NameId { get; set; }
    public int DescriptionId { get; set; }

    public bool PlayerCanJoin { get; set; }
    public bool IsFeatured { get; set; }

    public int PreferredRequirementId { get; set; }

    public int TutorialActivityId { get; set; }

    public bool MembersOnly { get; set; }

    public string? DetailImageFilename { get; set; }
    public string? ThumbnailImageFilename { get; set; }

    public int Difficulty { get; set; }

    public int MysteryChestIcon { get; set; }
    public int MysteryChestId { get; set; }

    public Dictionary<int, FeaturedActivityEntry> FeaturedActivities { get; set; } = [];

    public class FeaturedActivityEntry : ISerializableType
    {
        public long StartEpoch { get; set; }
        public long StopEpoch { get; set; }

        public int BonusRewardSetId { get; set; }

        public bool UsingServerTime { get; set; }

        public int ScheduledId { get; set; }

        public int BonusRewardTooltipStringId { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(StartEpoch);
            writer.Write(StopEpoch);

            writer.Write(BonusRewardSetId);

            writer.Write(UsingServerTime);

            writer.Write(ScheduledId);

            writer.Write(BonusRewardTooltipStringId);
        }
    }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(AppSystemId);

        writer.Write(Category);

        writer.Write(ImageSetId);

        writer.Write(ImagePositionId);

        writer.Write(DisplayNameId);
        writer.Write(DisplayDescriptionId);

        writer.Write(NameId);
        writer.Write(DescriptionId);

        writer.Write(ServerType);

        writer.Write(PlayerCanJoin);

        writer.Write(PreferredRequirementId);

        writer.Write(FeaturedActivities);

        writer.Write(TutorialActivityId);

        writer.Write(MembersOnly);

        writer.Write(DetailImageFilename);
        writer.Write(ThumbnailImageFilename);

        writer.Write(Difficulty);

        writer.Write(Unknown3);
        writer.Write(MysteryChestId);

        writer.Write(MysteryChestIcon);
    }
}