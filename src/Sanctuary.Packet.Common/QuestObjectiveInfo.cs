using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class QuestObjectiveInfo : ISerializableType, IDeserializableType
{
    public int ObjectiveId;
    public int NameId;
    public int DescriptionId;

    public bool IsBonus;

    public RewardBundleBase RewardBundle = RewardBundleBase.CreateEmpty();

    public ObjectiveState State;
    public int CurrentCount;
    public int RequiredCount;

    public int FailureCondition;
    public int Category;

    public bool MembersOnly;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(ObjectiveId);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(IsBonus);

        RewardBundle.Serialize(writer);

        writer.Write((int)State);
        writer.Write(CurrentCount);
        writer.Write(RequiredCount);
        writer.Write(FailureCondition);
        writer.Write(MembersOnly);
        writer.Write(Category);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out ObjectiveId))
            return false;

        if (!reader.TryRead(out NameId))
            return false;

        if (!reader.TryRead(out DescriptionId))
            return false;

        if (!reader.TryRead(out IsBonus))
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

        if (!reader.TryRead(out FailureCondition))
            return false;

        if (!reader.TryRead(out MembersOnly))
            return false;

        if (!reader.TryRead(out Category))
            return false;

        return true;
    }
}
