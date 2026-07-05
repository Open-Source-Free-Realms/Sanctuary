using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketPlaceFixture : BaseHousingPacket, IDeserializable<ClientHousingPacketPlaceFixture>
{
    public new const short OpCode = 2;

    public int ItemDefinitionId;
    public ulong FixtureGuid;
    public Vector4 Position;
    public Quaternion Rotation;

    public ClientHousingPacketPlaceFixture() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketPlaceFixture value)
    {
        value = new ClientHousingPacketPlaceFixture();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (reader.RemainingLength >= sizeof(int))
            reader.TryRead(out value.ItemDefinitionId);

        if (reader.RemainingLength >= sizeof(ulong))
            reader.TryRead(out value.FixtureGuid);

        if (reader.RemainingLength >= sizeof(float) * 4)
            reader.TryRead(out value.Position);

        if (reader.RemainingLength >= sizeof(float) * 4)
            reader.TryRead(out value.Rotation);

        return true;
    }
}
