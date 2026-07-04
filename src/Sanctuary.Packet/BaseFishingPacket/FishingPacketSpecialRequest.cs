using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class FishingPacketSpecialRequest : BaseFishingPacket, ISerializablePacket, IDeserializable<FishingPacketSpecialRequest>
{
    public new const short OpCode = 15;

    public ulong Guid;
    public ulong Data;

    public FishingPacketSpecialRequest() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Guid);
        writer.Write(Data);
        return writer.Buffer;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out FishingPacketSpecialRequest value)
    {
        value = new();
        var r = new PacketReader(data);
        if (!value.TryRead(ref r)) return false;
        if (!r.TryRead(out ulong g)) return false; value.Guid = g;
        if (!r.TryRead(out ulong d)) return false; value.Data = d;
        return true;
    }
}
