using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryChest : RewardBundleEntryBase
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.Chest;

    public int ItemGuid;

    protected override bool IsDataOptional => true;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(ItemGuid);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!reader.TryRead(out ItemGuid))
            return false;

        return true;
    }
}
