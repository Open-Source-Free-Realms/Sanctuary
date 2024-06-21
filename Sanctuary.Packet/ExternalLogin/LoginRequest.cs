using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class LoginRequest : IDeserializable<LoginRequest>
{
    public const byte OpCode = 1;

    public string? Session;
    public string? Fingerprint;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out LoginRequest value)
    {
        value = new LoginRequest();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out byte opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Session))
            return false;

        if (!reader.TryRead(out value.Fingerprint))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"{nameof(Session)}: \"{Session}\", " +
               $"{nameof(Fingerprint)}: \"{Fingerprint}\"";
    }
}