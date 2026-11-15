using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketRequestGrant : BaseHousingPacket, IDeserializable<ClientHousingPacketRequestGrant>
{
    public new const short OpCode = 23;

    public ClientHousingPacketRequestGrant() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketRequestGrant value)
    {
        value = new ClientHousingPacketRequestGrant();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}