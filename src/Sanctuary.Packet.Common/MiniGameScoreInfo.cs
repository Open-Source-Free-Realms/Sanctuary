using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class MiniGameScoreInfo : ISerializableType, IDeserializableType
{
    public string? Name;

    public int Unknown;

    public int Unknown2;

    public int Unknown3;

    public int Unknown4;

    public int Unknown5;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Name);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
    }

    public bool TryRead(ref PacketReader reader)
    {
        var nameReader = reader;

        if (!nameReader.TryRead(out int nameLength) || nameLength < 0 || nameLength > nameReader.RemainingLength)
            return false;

        if (!reader.TryRead(out Name))
            return false;

        if (!reader.TryRead(out Unknown))
            return false;

        if (!reader.TryRead(out Unknown2))
            return false;

        if (!reader.TryRead(out Unknown3))
            return false;

        if (!reader.TryRead(out Unknown4))
            return false;

        if (!reader.TryRead(out Unknown5))
            return false;

        return true;
    }
}
