using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryFactoryExperienceInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.FactoryExperience;

    protected override void SerializeData(PacketWriter writer)
    {

    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        return true;
    }
}
