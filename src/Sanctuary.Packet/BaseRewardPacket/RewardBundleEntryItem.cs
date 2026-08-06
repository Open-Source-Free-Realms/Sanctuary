using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public sealed class RewardBundleEntryItem : RewardBundleEntry
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.Item;

    public int ItemGuid;

    protected override void SerializeData(PacketWriter writer)
    {
        // Success controls the client's subtype-data flag; it is not another wire field.
        writer.Write(ItemGuid);
    }
}
