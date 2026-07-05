using System;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketSaveFixture : BaseHousingPacket, IDeserializable<ClientHousingPacketSaveFixture>
{
    public new const short OpCode = 5;

    public ulong FixtureGuid;
    public Vector4 Position;
    public Quaternion Rotation;

    public ClientHousingPacketSaveFixture() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketSaveFixture value)
    {
        value = new ClientHousingPacketSaveFixture();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (reader.RemainingLength >= sizeof(ulong))
            reader.TryRead(out value.FixtureGuid);

        if (reader.RemainingLength >= sizeof(float) * 4)
            reader.TryRead(out value.Position);

        if (reader.RemainingLength >= sizeof(float) * 4)
            reader.TryRead(out value.Rotation);

        return true;
    }
}
