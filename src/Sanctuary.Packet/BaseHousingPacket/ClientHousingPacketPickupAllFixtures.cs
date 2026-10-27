using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketPickupAllFixtures : BaseHousingPacket, IDeserializable<ClientHousingPacketPickupAllFixtures>
{
    public new const short OpCode = 4;

    public ClientHousingPacketPickupAllFixtures() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketPickupAllFixtures value)
    {
        value = new ClientHousingPacketPickupAllFixtures();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        return reader.RemainingLength == 0;
    }
}