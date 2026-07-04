using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class FishingPacketCastAnimRequest : BaseFishingPacket, ISerializablePacket, IDeserializable<FishingPacketCastAnimRequest>
{
    public new const short OpCode = 5;

    public ulong Guid;
    public int UnknownInt;
    public Vector4 Position;

    public FishingPacketCastAnimRequest() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Guid);
        writer.Write(UnknownInt);
        writer.Write(Position);
        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FishingPacketCastAnimRequest value)
    {
        value = new();
        var r = new PacketReader(data);
        if (!value.TryRead(ref r)) return false;
        if (!r.TryRead(out ulong g)) return false; value.Guid = g;
        if (!r.TryRead(out int i)) return false; value.UnknownInt = i;
        if (!r.TryRead(out Vector4 p)) return false; value.Position = p;
        return true;
    }
}
