using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketMountRequest : MountBasePacket, IDeserializable<PacketMountRequest>
{
    public new const byte OpCode = 1;

    public ulong MountGuid;
    public int Seat;

    public byte Unknown;
    public byte Unknown2;

    public PacketMountRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketMountRequest value)
    {
        value = new PacketMountRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.MountGuid))
            return false;

        if (!reader.TryRead(out value.Seat))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        return reader.RemainingLength == 0;
    }
}
