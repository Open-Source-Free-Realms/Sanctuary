using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryQuestAddInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.QuestAdd;

    public int QuestId;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(QuestId);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!reader.TryRead(out QuestId))
            return false;
        return true;
    }
}
