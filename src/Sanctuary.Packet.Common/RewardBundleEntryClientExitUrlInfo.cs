using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryClientExitUrlInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.ClientExitUrl;

    protected override void SerializeData(PacketWriter writer)
    {

    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        return true;
    }
}
