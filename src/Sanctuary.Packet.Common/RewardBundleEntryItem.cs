using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryItem : RewardBundleEntryBase
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.Item;

    public int ItemGuid;

    protected override void SerializeData(PacketWriter writer)
    {
        // The inherited common fields are serialized first; the item-specific payload is only its GUID.
        writer.Write(ItemGuid);
    }
}
