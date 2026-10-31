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

    protected virtual bool IsDataOptional => false;

    internal void Serialize(PacketWriter writer, bool hasOptionalData)
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

        if (hasOptionalData || !IsDataOptional)
            SerializeData(writer);
    }

    internal bool TryRead(ref PacketReader reader, bool hasOptionalData)
    {
        if (!reader.TryRead(out IsHidden))
            return false;

        if (!reader.TryRead(out IconId))
            return false;

        if (!reader.TryRead(out TintId))
            return false;

        if (!reader.TryRead(out NameId))
            return false;

        if (!reader.TryRead(out Quantity))
            return false;

        if (!reader.TryRead(out DefinitionId))
            return false;

        if (!reader.TryRead(out Tint))
            return false;

        if (!reader.TryRead(out Description))
            return false;

        if (!reader.TryRead(out ItemTextColor))
            return false;

        if (!reader.TryRead(out MembersOnly))
            return false;

        if (!hasOptionalData && IsDataOptional)
            return true;

        return TryReadData(ref reader);
    }

    protected virtual void SerializeData(PacketWriter writer)
    {
    }

    protected virtual bool TryReadData(ref PacketReader reader)
    {
        return true;
    }
}
