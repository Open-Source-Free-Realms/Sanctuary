using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryTokenInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.Token;

    protected override void SerializeData(PacketWriter writer)
    {

    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        return true;
    }
}
