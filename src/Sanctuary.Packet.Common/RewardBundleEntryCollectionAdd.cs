using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryCollectionAdd : RewardBundleEntryBase
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.CollectionAdd;

    public int CollectionId;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(CollectionId);
    }
}
