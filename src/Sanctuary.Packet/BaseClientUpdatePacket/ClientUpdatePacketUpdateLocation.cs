using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientUpdatePacketUpdateLocation : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 12;

    public Vector4 Position;
    public Quaternion Rotation;

    public bool Teleport;
    public byte Unknown;

    public ClientUpdatePacketUpdateLocation() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Position);
        writer.Write(Rotation);

        writer.Write(Teleport);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}