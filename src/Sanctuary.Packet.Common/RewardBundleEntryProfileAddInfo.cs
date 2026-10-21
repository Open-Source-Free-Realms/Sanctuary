using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryProfileAddInfo : RewardBundleEntryInfo
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.ProfileAdd;

    public int ProfileId;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(ProfileId);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!reader.TryRead(out ProfileId))
            return false;
        return true;
    }
}
