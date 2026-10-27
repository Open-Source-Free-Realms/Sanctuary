using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class QuestObjectiveInfo : ISerializableType
{
    public int ObjectiveId { get; set; }
    public int NameId { get; set; }
    public int DescriptionId { get; set; }

    // Never read by the client.
    public bool Unknown { get; set; }

    public RewardBundleBase RewardBundle { get; set; } = new()
    {
        Success = false,
        Unknown3 = 0,
        IconId = -1,
        NameId = -1
    };

    // 0 = inactive, 1 = active, 2 = complete, 3 = failed.
    public int State { get; set; }
    public int CurrentCount { get; set; }
    public int RequiredCount { get; set; }

    // Never read by the client.
    public int Unknown6 { get; set; }
    public bool Unknown7 { get; set; }
    public int Unknown8 { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(ObjectiveId);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(Unknown);

        RewardBundle.Serialize(writer);

        writer.Write(State);
        writer.Write(CurrentCount);
        writer.Write(RequiredCount);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(Unknown8);
    }
}
