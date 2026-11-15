using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GroupPacketGroupKick : BaseGroupPacket, ISerializablePacket, IDeserializable<GroupPacketGroupKick>
{
    public new const short OpCode = 6;

    public ulong KickerGuid;
    public NameData KickerName = new();

    public ulong TargetGuid;
    public NameData TargetName = new();

    public int Unknown;

    public bool DeferMessage;

    public GroupPacketGroupKick() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(KickerGuid);
        KickerName.Serialize(writer);

        writer.Write(TargetGuid);
        TargetName.Serialize(writer);

        writer.Write(Unknown);

        writer.Write(DeferMessage);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupPacketGroupKick value)
    {
        value = new GroupPacketGroupKick();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.KickerGuid))
            return false;

        if (!value.KickerName.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.TargetGuid))
            return false;

        if (!value.TargetName.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.DeferMessage))
            return false;

        return reader.RemainingLength == 0;
    }
}
