using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryTrophyInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.Trophy;

    protected override void SerializeData(PacketWriter writer)
    {

    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        return true;
    }
}
