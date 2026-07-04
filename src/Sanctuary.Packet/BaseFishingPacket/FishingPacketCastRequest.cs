using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class FishingPacketCastRequest : BaseFishingPacket, ISerializablePacket, IDeserializable<FishingPacketCastRequest>
{
    public new const short OpCode = 6;

    public ulong Guid;
    public Vector4 Position;
    public bool Flag;

    public FishingPacketCastRequest() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Guid);
        writer.Write(Position);
        writer.Write(Flag);
        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FishingPacketCastRequest value)
    {
        value = new();
        var r = new PacketReader(data);
        if (!value.TryRead(ref r)) return false;
        if (!r.TryRead(out ulong g)) return false; value.Guid = g;
        if (!r.TryRead(out Vector4 p)) return false; value.Position = p;
        if (!r.TryRead(out byte f)) return false; value.Flag = f != 0;
        return true;
    }
}
