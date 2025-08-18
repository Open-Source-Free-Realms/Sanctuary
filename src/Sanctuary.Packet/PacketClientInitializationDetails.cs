using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketClientInitializationDetails : IDeserializable<PacketClientInitializationDetails>
{
    public const short OpCode = 169;

    public int TimezoneOffset;

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out PacketClientInitializationDetails value)
    {
        value = new PacketClientInitializationDetails();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) && opCode != OpCode)
            return false;

        if (!reader.TryRead(out value.TimezoneOffset))
            return false;

        return reader.RemainingLength == 0;
    }
}