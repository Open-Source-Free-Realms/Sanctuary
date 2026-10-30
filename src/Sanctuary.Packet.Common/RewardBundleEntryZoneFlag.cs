using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryZoneFlag : RewardBundleEntryBase
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.ZoneFlag;

    public string ZoneFlag = string.Empty;
    public int Value;
    public int FlagType;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(ZoneFlag);
        writer.Write(Value);
        writer.Write(FlagType);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!reader.TryRead(out ZoneFlag))
            return false;

        if (!reader.TryRead(out Value))
            return false;

        if (!reader.TryRead(out FlagType))
            return false;

        return true;
    }
}
