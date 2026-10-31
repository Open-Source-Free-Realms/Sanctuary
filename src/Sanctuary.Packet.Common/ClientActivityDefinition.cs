using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientActivityDefinition : ISerializableType, IDeserializableType
{
    public int Id;

    public int AppSystemId;

    public int AppSystemDataId;

    public int Category;

    public int ImageSetId;

    public int ImagePositionId;

    public int ServerType;

    public int DisplayNameId;
    public int DisplayDescriptionId;

    public int NameId;
    public int DescriptionId;

    public bool PlayerCanJoin;

    public int PreferredRequirementId;

    public int TutorialActivityId;

    public bool MembersOnly;

    public string? DetailImageFilename;
    public string? ThumbnailImageFilename;

    public int Difficulty;

    public int MysteryChestIcon;
    public int MysteryChestId;

    public Dictionary<int, FeaturedActivityEntry> FeaturedActivities = [];

    public class FeaturedActivityEntry : ISerializableType, IDeserializableType
    {
        public long StartEpoch;
        public long StopEpoch;

        public int BonusRewardSetId;

        public bool UsingServerTime;

        public int ScheduledId;

        public int BonusRewardTooltipStringId;
        public void Serialize(PacketWriter writer)
        {
            writer.Write(StartEpoch);
            writer.Write(StopEpoch);
            writer.Write(BonusRewardSetId);
            writer.Write(UsingServerTime);
            writer.Write(ScheduledId);
            writer.Write(BonusRewardTooltipStringId);
        }


        public bool TryRead(ref PacketReader reader)
        {
            if (!reader.TryRead(out StartEpoch))
                return false;

            if (!reader.TryRead(out StopEpoch))
                return false;

            if (!reader.TryRead(out BonusRewardSetId))
                return false;

            if (!reader.TryRead(out UsingServerTime))
                return false;

            if (!reader.TryRead(out ScheduledId))
                return false;

            if (!reader.TryRead(out BonusRewardTooltipStringId))
                return false;

            return true;
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
        writer.Write(AppSystemDataId);
        writer.Write(MysteryChestId);
        writer.Write(MysteryChestIcon);
    }

    public ClientActivityDefinition Clone() => (ClientActivityDefinition)MemberwiseClone();

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out Id))
            return false;

        if (!reader.TryRead(out AppSystemId))
            return false;

        if (!reader.TryRead(out Category))
            return false;

        if (!reader.TryRead(out ImageSetId))
            return false;

        if (!reader.TryRead(out ImagePositionId))
            return false;

        if (!reader.TryRead(out DisplayNameId))
            return false;

        if (!reader.TryRead(out DisplayDescriptionId))
            return false;

        if (!reader.TryRead(out NameId))
            return false;

        if (!reader.TryRead(out DescriptionId))
            return false;

        if (!reader.TryRead(out ServerType))
            return false;

        if (!reader.TryRead(out PlayerCanJoin))
            return false;

        if (!reader.TryRead(out PreferredRequirementId))
            return false;

        if (!reader.TryRead(out int count) || count < 0)
            return false;

        FeaturedActivities = [];

        for (int i = 0; i < count; i++)
        {
            if (!reader.TryRead(out int id))
                return false;

            var featuredActivityEntry = new FeaturedActivityEntry();

            if (!featuredActivityEntry.TryRead(ref reader))
                return false;

            if (!FeaturedActivities.TryAdd(id, featuredActivityEntry))
                return false;
        }

        if (!reader.TryRead(out TutorialActivityId))
            return false;

        if (!reader.TryRead(out MembersOnly))
            return false;

        if (!reader.TryRead(out DetailImageFilename))
            return false;

        if (!reader.TryRead(out ThumbnailImageFilename))
            return false;

        if (!reader.TryRead(out Difficulty))
            return false;

        if (!reader.TryRead(out AppSystemDataId))
            return false;

        if (!reader.TryRead(out MysteryChestId))
            return false;

        if (!reader.TryRead(out MysteryChestIcon))
            return false;

        return true;
    }
}