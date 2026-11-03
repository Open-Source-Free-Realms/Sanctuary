using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientQuest : ISerializableType
{
    public int QuestId;
    public int TitleId;
    public int DescriptionId;
    public int HelperTextId;
    public bool MembersOnly;
    public long TimeStarted;
    public int ProfileId;
    public bool SuppressStartBanner;
    public float CompletedPercentage;

    public RewardBundleBase RewardBundle { get; set; } = new()
    {
        Success = false,
        Unknown3 = 0,
        Multiplier = 0f
    };

    public Dictionary<int, QuestObjectiveInfo> Objectives { get; set; } = [];

    public int IconId;
    public bool SystemQuest;
    // ClientQuestData::m_bUnknown; never read by the client.
    public bool Unknown2;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(QuestId);
        writer.Write(TitleId);
        writer.Write(DescriptionId);
        writer.Write(HelperTextId);
        writer.Write(MembersOnly);
        writer.Write(TimeStarted);
        writer.Write(ProfileId);
        writer.Write(SuppressStartBanner);
        writer.Write(CompletedPercentage);

        RewardBundle.Serialize(writer);

        writer.Write(Objectives);

        writer.Write(IconId);
        writer.Write(SystemQuest);
        writer.Write(Unknown2);
    }
}
