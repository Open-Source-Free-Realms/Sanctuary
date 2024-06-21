using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketLogin : IDeserializable<PacketLogin>
{
    public const short OpCode = 1;

    public string? Ticket;
    public ulong Guid;
    public string? Version;
    public string? Unknown;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketLogin value)
    {
        value = new PacketLogin();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.Ticket))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        if (!reader.TryRead(out value.Version))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"( {nameof(Ticket)}: \"{Ticket}\", {nameof(Guid)}: {Guid}, {nameof(Version)}: \"{Version}\", {nameof(Unknown)}: \"{Unknown}\" )";
    }
}