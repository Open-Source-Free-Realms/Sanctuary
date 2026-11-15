using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GroupPacketGroupAccept : BaseGroupPacket, ISerializablePacket, IDeserializable<GroupPacketGroupAccept>
{
    public new const short OpCode = 4;

    public GroupInviteInfo Info = new();

    public bool Accepted = true;

    public int Unknown;
    public int Unknown2;
    public int Unknown3;
    public int Unknown4;
    public int Unknown5;

    public GroupPacketGroupAccept() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Info.Serialize(writer);

        writer.Write(Accepted);

        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupPacketGroupAccept value)
    {
        value = new GroupPacketGroupAccept();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!value.Info.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Accepted))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        if (!reader.TryRead(out value.Unknown3))
            return false;

        if (!reader.TryRead(out value.Unknown4))
            return false;

        if (!reader.TryRead(out value.Unknown5))
            return false;

        return reader.RemainingLength == 0;
    }
}
