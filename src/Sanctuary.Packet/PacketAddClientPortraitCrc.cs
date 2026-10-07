using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketAddClientPortraitCrc : ISerializablePacket, IDeserializable<PacketAddClientPortraitCrc>
{
    public const short OpCode = 115;

    public uint PortraitCrc;

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(PortraitCrc);
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketAddClientPortraitCrc value)
    {
        value = new PacketAddClientPortraitCrc();
        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.PortraitCrc))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString() => $"{nameof(PortraitCrc)}: {PortraitCrc}";
}
