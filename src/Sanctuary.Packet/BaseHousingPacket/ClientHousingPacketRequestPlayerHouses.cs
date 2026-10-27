using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketRequestPlayerHouses : BaseHousingPacket, IDeserializable<ClientHousingPacketRequestPlayerHouses>
{
    public new const short OpCode = 14;

    public ulong PlayerGuid;
    public string PlayerName = null!;

    public ClientHousingPacketRequestPlayerHouses() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketRequestPlayerHouses value)
    {
        value = new ClientHousingPacketRequestPlayerHouses();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.PlayerGuid))
            return false;

        if (!reader.TryRead(out value.PlayerName))
            return false;

        return reader.RemainingLength == 0;
    }
}