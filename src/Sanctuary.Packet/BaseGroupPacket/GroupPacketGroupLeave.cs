using System;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class GroupPacketGroupLeave : BaseGroupPacket, ISerializablePacket, IDeserializable<GroupPacketGroupLeave>
{
    public new const short OpCode = 3;

    public ulong PlayerGuid;
    public NameData PlayerName = new();

    public GroupPacketGroupLeave() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PlayerGuid);
        PlayerName.Serialize(writer);

        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupPacketGroupLeave value)
    {
        value = new GroupPacketGroupLeave();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PlayerGuid))
            return false;

        if (!value.PlayerName.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}
