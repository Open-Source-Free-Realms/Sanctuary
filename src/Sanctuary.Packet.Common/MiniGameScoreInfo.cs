using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class MiniGameScoreInfo : ISerializableType, IDeserializableType
{
    public string? Name;

    public int IconId;

    public int Type;

    public int Count;

    public int MaxCount;

    public int Points;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Name);
        writer.Write(IconId);
        writer.Write(Type);
        writer.Write(Count);
        writer.Write(MaxCount);
        writer.Write(Points);
    }

    public bool TryRead(ref PacketReader reader)
    {
        var nameReader = reader;

        if (!nameReader.TryRead(out int nameLength) || nameLength < 0 || nameLength > nameReader.RemainingLength)
            return false;

        if (!reader.TryRead(out Name))
            return false;

        if (!reader.TryRead(out IconId))
            return false;

        if (!reader.TryRead(out Type))
            return false;

        if (!reader.TryRead(out Count))
            return false;

        if (!reader.TryRead(out MaxCount))
            return false;

        if (!reader.TryRead(out Points))
            return false;

        return true;
    }
}
