using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketPickupFixture : BaseHousingPacket, IDeserializable<ClientHousingPacketPickupFixture>
{
    public new const short OpCode = 3;

    public ulong FixtureGuid;

    public ClientHousingPacketPickupFixture() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketPickupFixture value)
    {
        value = new ClientHousingPacketPickupFixture();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.FixtureGuid))
            return false;

        return reader.RemainingLength == 0;
    }
}
