using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class HousingPacketUpdateFixturePosition : BaseHousingPacket, ISerializablePacket
{
    public new const short OpCode = 51;

    public ulong FixtureGuid;

    public Vector4 Position;
    public Quaternion Rotation;

    public HousingPacketUpdateFixturePosition() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(FixtureGuid);

        writer.Write(Position);
        writer.Write(Rotation);

        return writer.Buffer;
    }
}