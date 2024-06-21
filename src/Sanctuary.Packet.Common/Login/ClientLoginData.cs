using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientLoginData : IDeserializable<ClientLoginData>
{
    public string? Locale;

    public int LocaleId;

    public bool Unknown;
    public byte Unknown2;
    public long Unknown3;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientLoginData value)
    {
        value = new ClientLoginData();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out value.Locale))
            return false;

        if (!reader.TryRead(out value.LocaleId))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!reader.TryRead(out value.Unknown2))
            return false;

        if (!reader.TryRead(out value.Unknown3))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"{nameof(Locale)}: \"{Locale}\", " +
               $"{nameof(LocaleId)}: {LocaleId}, " +
               $"{nameof(Unknown)}: {Unknown}, " +
               $"{nameof(Unknown2)}: {Unknown2}, " +
               $"{nameof(Unknown3)}: {Unknown3}";
    }
}