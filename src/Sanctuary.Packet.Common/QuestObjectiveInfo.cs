using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class QuestObjectiveInfo : ISerializableType, IDeserializableType
{
    public int ObjectiveId;
    public int NameId;
    public int DescriptionId;

    // Never read by the client.
    public bool Unknown;

    public RewardBundleBase RewardBundle = RewardBundleBase.CreateEmpty();

    public ObjectiveState State;
    public int CurrentCount;
    public int RequiredCount;

    // Never read by the client.
    public int Unknown6;

    public bool MembersOnly;

    // Never read by the client.
    public int Unknown8;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(ObjectiveId);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(Unknown);

        RewardBundle.Serialize(writer);

        writer.Write((int)State);
        writer.Write(CurrentCount);
        writer.Write(RequiredCount);
        writer.Write(Unknown6);
        writer.Write(MembersOnly);
        writer.Write(Unknown8);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out ObjectiveId))
            return false;

        if (!reader.TryRead(out NameId))
            return false;

        if (!reader.TryRead(out DescriptionId))
            return false;

        if (!reader.TryRead(out Unknown))
            return false;

        if (!RewardBundle.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out int state))
            return false;

        State = (ObjectiveState)state;

        if (!reader.TryRead(out CurrentCount))
            return false;

        if (!reader.TryRead(out RequiredCount))
            return false;

        if (!reader.TryRead(out Unknown6))
            return false;

        if (!reader.TryRead(out MembersOnly))
            return false;

        if (!reader.TryRead(out Unknown8))
            return false;

        return true;
    }
}
