using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryCollectionEntryAdd : RewardBundleEntryBase
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.CollectionEntryAdd;

    public int EntryType;
    public int EntryParam1;
    public int EntryParam2;
    public int EntryParam3;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(EntryType);
        writer.Write(EntryParam1);
        writer.Write(EntryParam2);
        writer.Write(EntryParam3);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!reader.TryRead(out EntryType))
            return false;

        if (!reader.TryRead(out EntryParam1))
            return false;

        if (!reader.TryRead(out EntryParam2))
            return false;

        if (!reader.TryRead(out EntryParam3))
            return false;

        return true;
    }
}
