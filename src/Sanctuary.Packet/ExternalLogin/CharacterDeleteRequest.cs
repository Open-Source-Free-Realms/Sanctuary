using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CharacterDeleteRequest : IDeserializable<CharacterDeleteRequest>
{
    public const byte OpCode = 9;

    public ulong EntityKey;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CharacterDeleteRequest value)
    {
        value = new CharacterDeleteRequest();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.EntityKey))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"{nameof(EntityKey)}: {EntityKey}";
    }
}