using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryAbilityLineInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.AbilityLine;

    public int AbilityLineId;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(AbilityLineId);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!reader.TryRead(out AbilityLineId))
            return false;
        return true;
    }
}
