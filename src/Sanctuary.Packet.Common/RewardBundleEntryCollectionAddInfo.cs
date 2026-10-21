using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryCollectionAddInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.CollectionAdd;

    public int CollectionId;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(CollectionId);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!reader.TryRead(out CollectionId))
            return false;
        return true;
    }
}
