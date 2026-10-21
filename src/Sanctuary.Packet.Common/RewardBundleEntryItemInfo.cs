using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryItemInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.Item;

    public int ItemGuid;

    protected override void SerializeData(PacketWriter writer)
    {
        if (!HasOptionalData)
            return;

        writer.Write(ItemGuid);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!HasOptionalData)
            return true;

        if (!reader.TryRead(out ItemGuid))
            return false;
        return true;
    }
}
