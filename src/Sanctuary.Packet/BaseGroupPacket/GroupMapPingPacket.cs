using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GroupMapPingPacket : BaseGroupPacket, ISerializablePacket, IDeserializable<GroupMapPingPacket>
{
    public new const short OpCode = 16;

    public ulong SenderGuid;

    public int X;
    public int Y;

    public GroupMapPingPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(SenderGuid);

        writer.Write(X);
        writer.Write(Y);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupMapPingPacket value)
    {
        value = new GroupMapPingPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.SenderGuid))
            return false;

        if (!reader.TryRead(out value.X))
            return false;

        if (!reader.TryRead(out value.Y))
            return false;

        return reader.RemainingLength == 0;
    }
}
