using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public abstract class RewardBundleEntryInfo : ISerializableType, IDeserializableType
{
    public abstract RewardBundleEntryType Type { get; }

    public bool HasOptionalData;
    public bool IsHidden;
    public int IconId;
    public int TintId;
    public int NameId;
    public int Quantity;
    public int DefinitionId;
    public int Tint;
    public string Description = string.Empty;
    public int ItemTextColor;
    public bool MembersOnly;

    public void Serialize(PacketWriter writer)
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

    public bool TryRead(ref PacketReader reader)
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

        var stringReader = reader;
        if (!stringReader.TryRead(out int stringLength) || stringLength < 0 || stringLength > stringReader.RemainingLength)
            return false;

        if (!reader.TryRead(out Description))
            return false;

        if (!reader.TryRead(out ItemTextColor))
            return false;

        if (!reader.TryRead(out MembersOnly))
            return false;
        return TryReadData(ref reader);
    }

    protected abstract void SerializeData(PacketWriter writer);

    protected abstract bool TryReadData(ref PacketReader reader);
}
