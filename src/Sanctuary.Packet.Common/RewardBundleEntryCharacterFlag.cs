using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class RewardBundleEntryCharacterFlag : RewardBundleEntryBase
{
    public override RewardBundleEntryType Type => RewardBundleEntryType.CharacterFlag;

    public string Key = string.Empty;
    public int Operation;
    public int Amount;
    public bool Persistent;
    public int Unknown4;
    public bool CountTimeOffline;

    protected override void SerializeData(PacketWriter writer)
    {
        writer.Write(Key);
        writer.Write(Operation);
        writer.Write(Amount);
        writer.Write(Persistent);
        writer.Write(Unknown4);
        writer.Write(CountTimeOffline);
    }

    protected override bool TryReadData(ref PacketReader reader)
    {
        if (!reader.TryRead(out Key))
            return false;

        if (!reader.TryRead(out Operation))
            return false;

        if (!reader.TryRead(out Amount))
            return false;

        if (!reader.TryRead(out Persistent))
            return false;

        if (!reader.TryRead(out Unknown4))
            return false;

        if (!reader.TryRead(out CountTimeOffline))
            return false;

        return true;
    }
}
