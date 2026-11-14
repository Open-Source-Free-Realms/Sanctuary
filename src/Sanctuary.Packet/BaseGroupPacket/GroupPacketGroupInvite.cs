using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GroupPacketGroupInvite : BaseGroupPacket, ISerializablePacket, IDeserializable<GroupPacketGroupInvite>
{
    public new const short OpCode = 1;

    public GroupInviteInfo Info = new();

    public int Unknown;
    public int Unknown2;
    public int Unknown3;
    public int Unknown4;
    public int Unknown5;

    public GroupPacketGroupInvite() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Info.Serialize(writer);

        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupPacketGroupInvite value)
    {
        value = new GroupPacketGroupInvite();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!value.Info.TryRead(ref reader))
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
