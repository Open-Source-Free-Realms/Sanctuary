using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class AchievementObjectiveInfo : ISerializableType, IDeserializableType
{
    public int Id;

    public int NameId;

    public int DescriptionId;

    public bool Unknown4;

    public RewardBundleInfo RewardBundle = new();

    public int Status;

    public int Count;

    public int Total;

    public int Unknown8;

    public bool MembersOnly;

    public int Unknown10;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(Unknown4);
        RewardBundle.Serialize(writer);
        writer.Write(Status);
        writer.Write(Count);
        writer.Write(Total);
        writer.Write(Unknown8);
        writer.Write(MembersOnly);
        writer.Write(Unknown10);

    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out Id))
            return false;

        if (!reader.TryRead(out NameId))
            return false;

        if (!reader.TryRead(out DescriptionId))
            return false;

        if (!reader.TryRead(out Unknown4))
            return false;

        if (!RewardBundle.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out Status))
            return false;

        if (!reader.TryRead(out Count))
            return false;

        if (!reader.TryRead(out Total))
            return false;

        if (!reader.TryRead(out Unknown8))
            return false;

        if (!reader.TryRead(out MembersOnly))
            return false;

        if (!reader.TryRead(out Unknown10))
            return false;

        return true;
    }
}
