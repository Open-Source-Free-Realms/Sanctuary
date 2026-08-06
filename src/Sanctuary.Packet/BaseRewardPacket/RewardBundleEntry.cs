using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public abstract class RewardBundleEntry
{
    public abstract RewardBundleEntryType Type { get; }

    public bool Unknown;
    public int IconId;
    public int IconTintId;
    public int NameId;
    public int Quantity = 1;
    public int DefinitionId;
    public int Tint;
    public string Text = string.Empty;
    public int Unknown2;
    public bool Unknown3;

    internal void Serialize(PacketWriter writer)
    {
        writer.Write((int)Type);
        writer.Write(Unknown);
        writer.Write(IconId);
        writer.Write(IconTintId);
        writer.Write(NameId);
        writer.Write(Quantity);
        writer.Write(DefinitionId);
        writer.Write(Tint);
        writer.Write(Text);
        writer.Write(Unknown2);
        writer.Write(Unknown3);

        SerializeData(writer);
    }

    protected abstract void SerializeData(PacketWriter writer);
}
