using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketMountSpawn : MountBasePacket, IDeserializable<PacketMountSpawn>
{
    public new const byte OpCode = 6;

    public int Id;

    public PacketMountSpawn() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketMountSpawn value)
    {
        value = new PacketMountSpawn();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Id))
            return false;

        return reader.RemainingLength == 0;
    }
}