using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketDismountRequest : MountBasePacket, IDeserializable<PacketDismountRequest>
{
    public new const byte OpCode = 3;

    public PacketDismountRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketDismountRequest value)
    {
        value = new PacketDismountRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}