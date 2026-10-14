using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientPathRequestPacket : ClientPathBasePacket, IDeserializable<ClientPathRequestPacket>
{
    public new const byte OpCode = 1;

    public ulong Guid;

    public int Type;
    public int Id;

    public Vector4 FromPosition;
    public Vector4 ToPosition;

    public int Unknown;

    public ClientPathRequestPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientPathRequestPacket value)
    {
        value = new ClientPathRequestPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Type))
            return false;

        if (!reader.TryRead(out value.Id))
            return false;

        if (!reader.TryRead(out value.FromPosition))
            return false;

        if (!reader.TryRead(out value.ToPosition))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"{nameof(Guid)}: {Guid}, {nameof(Type)}: {Type}, {nameof(Id)}: {Id}, {nameof(FromPosition)}: {FromPosition}, {nameof(ToPosition)}: {ToPosition}, {nameof(Unknown)}: {Unknown}";
    }
}
