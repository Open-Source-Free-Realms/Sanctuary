using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class InspectProxy
{
    public List<ProfileEntry> Profiles = [];

    public class ProfileEntry : ISerializableType
    {
        public int Id;
        public int Rank;

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Id);
            writer.Write(Rank);
        }
    }

    public int ActiveProfileId;

    public List<ItemEntry> Items = [];

    public class ItemEntry : ISerializableType
    {
        public int Slot;

        public ItemRecord ItemRecord = new();

        public ClientItemDefinition ItemDefinition = new();

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Slot);

            ItemRecord.Serialize(writer);
            ItemDefinition.Serialize(writer);
        }
    }

    // TODO: Achievements

    public float VipRank;
    public int VipIconId;
    public int VipTitle;

    public int ActiveTitle;

    public int Coins;

    public int LevelsGained;
    public int CollectionCount;
    public int QuestCount;

    public List<CharacterStat> Stats = [];

    public string? Unknown;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(Profiles);

        writer.Write(ActiveProfileId);

        writer.Write(Items);

        // Achievements
        writer.Write(0);
        writer.Write(0);

        writer.Write(VipRank);
        writer.Write(VipIconId);
        writer.Write(VipTitle);

        writer.Write(ActiveTitle);

        writer.Write(Coins);

        writer.Write(LevelsGained);
        writer.Write(CollectionCount);
        writer.Write(QuestCount);

        writer.Write(Stats);

        writer.Write(Unknown);

        return writer.Buffer;
    }
}