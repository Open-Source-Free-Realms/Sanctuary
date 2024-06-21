using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketCheckNameRequest : IDeserializable<PacketCheckNameRequest>
{
    public const short OpCode = 210;

    public string? FirstName;
    public string? LastName;

    public string? NameDefinition;
    public string? LastPrefixDefinition;
    public string? LastSuffixDefinition;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketCheckNameRequest value)
    {
        value = new PacketCheckNameRequest();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.FirstName))
            return false;

        if (!reader.TryRead(out value.LastName))
            return false;

        if (!reader.TryRead(out value.NameDefinition))
            return false;

        if (!reader.TryRead(out value.LastPrefixDefinition))
            return false;

        if (!reader.TryRead(out value.LastSuffixDefinition))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"{nameof(FirstName)}: \"{FirstName}\", {nameof(LastName)}: \"{LastName}\", {nameof(NameDefinition)}: \"{NameDefinition}\", {nameof(LastPrefixDefinition)}: \"{LastPrefixDefinition}\", {nameof(LastSuffixDefinition)}: \"{LastSuffixDefinition}\"";
    }
}