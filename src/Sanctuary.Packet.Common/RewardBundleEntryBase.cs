using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public abstract class RewardBundleEntryBase
{
    public abstract RewardBundleEntryType Type { get; }

    public bool IsHidden;
    public int IconId;
    public int TintId;
    public int NameId;
    public int Quantity = 1;
    public int DefinitionId;
    public int Tint;
    public string Description = string.Empty;
    public int ItemTextColor;
    public bool MembersOnly;

    internal void Serialize(PacketWriter writer)
    {
        writer.Write((int)Type);
        writer.Write(IsHidden);
        writer.Write(IconId);
        writer.Write(TintId);
        writer.Write(NameId);
        writer.Write(Quantity);
        writer.Write(DefinitionId);
        writer.Write(Tint);
        writer.Write(Description);
        writer.Write(ItemTextColor);
        writer.Write(MembersOnly);

        SerializeData(writer);
    }

    protected abstract void SerializeData(PacketWriter writer);
}
