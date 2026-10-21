using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryWheelSpinInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.WheelSpin;

    protected override void SerializeData(PacketWriter writer)
    {

    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        return true;
    }
}
