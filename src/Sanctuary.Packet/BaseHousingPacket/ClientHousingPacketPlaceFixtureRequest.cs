using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketPlaceFixtureRequest : BaseHousingPacket, IDeserializable<ClientHousingPacketPlaceFixtureRequest>
{
    public new const short OpCode = 1;

    public int ItemDefinitionId;

    public ClientHousingPacketPlaceFixtureRequest() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketPlaceFixtureRequest value)
    {
        value = new ClientHousingPacketPlaceFixtureRequest();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (reader.RemainingLength >= sizeof(int))
            reader.TryRead(out value.ItemDefinitionId);

        return true;
    }
}
