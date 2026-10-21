using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryPetTrickExperienceInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.PetTrickExperience;

    public int TrickId;
    public int Unknown2;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(TrickId);
        writer.Write(Unknown2);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!reader.TryRead(out TrickId))
            return false;

        if (!reader.TryRead(out Unknown2))
            return false;
        return true;
    }
}
