using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class FishingPacketReelInRequest : BaseFishingPacket, ISerializablePacket, IDeserializable<FishingPacketReelInRequest>
{
    public new const short OpCode = 7;

    public ulong Guid;
    public bool Flag;

    public FishingPacketReelInRequest() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Guid);
        writer.Write(Flag);
        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FishingPacketReelInRequest value)
    {
        value = new();
        var r = new PacketReader(data);
        if (!value.TryRead(ref r)) return false;
        if (!r.TryRead(out ulong g)) return false; value.Guid = g;
        if (!r.TryRead(out byte f)) return false; value.Flag = f != 0;
        return true;
    }
}
