using System;
using System.Numerics;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class ClientHousingPacketSaveFixture : BaseHousingPacket, IDeserializable<ClientHousingPacketSaveFixture>
{
    public new const short OpCode = 5;

    public ulong FixtureGuid;
    public Vector4 Position;
    public Quaternion Rotation;
    public float Unknown;
    public CustomizationDetail Customization = new();
    public float Scale;

    public ClientHousingPacketSaveFixture() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketSaveFixture value)
    {
        value = new ClientHousingPacketSaveFixture();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.FixtureGuid))
            return false;

        if (!reader.TryRead(out value.Position))
            return false;

        if (!reader.TryRead(out value.Rotation))
            return false;

        if (reader.RemainingLength == sizeof(float))
        {
            if (!reader.TryRead(out value.Scale))
                return false;

            return reader.RemainingLength == 0;
        }

        if (!reader.TryRead(out value.Unknown))
            return false;

        if (!value.Customization.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Scale))
            return false;

        return reader.RemainingLength == 0;
    }
}
