using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketPlaceFixture : BaseHousingPacket, IDeserializable<ClientHousingPacketPlaceFixture>
{
    public new const short OpCode = 2;

    public int ItemDefinitionId;
    public Vector4 Position;
    public Quaternion Rotation;
    public float Scale;

    public ClientHousingPacketPlaceFixture() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketPlaceFixture value)
    {
        value = new ClientHousingPacketPlaceFixture();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.ItemDefinitionId))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        if (!reader.TryRead(out value.Rotation))
            return false;

        if (!reader.TryRead(out value.Scale))
            return false;

        return reader.RemainingLength == 0;
    }
}
