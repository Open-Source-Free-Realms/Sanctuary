using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketRequestPlayerHouses : BaseHousingPacket, IDeserializable<ClientHousingPacketRequestPlayerHouses>
{
    public new const short OpCode = 14;

    public ulong PlayerGuid;
    public string? Search;

    public ClientHousingPacketRequestPlayerHouses() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketRequestPlayerHouses value)
    {
        value = new ClientHousingPacketRequestPlayerHouses();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (reader.RemainingLength == 0)
            return true;

        if (reader.RemainingLength >= sizeof(ulong) && !reader.TryRead(out value.PlayerGuid))
            return false;

        if (reader.RemainingLength > 0 && !reader.TryRead(out value.Search))
            return false;

        return true;
    }
}
