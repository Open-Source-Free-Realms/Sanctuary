using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketClientBeginZoning : ISerializablePacket
{
    public const short OpCode = 31;

    public string Name = null!;
    public int Type;

    public Vector4 Position;
    public Quaternion Rotation;

    public string? Sky;

    public bool Tutorial;
    public byte Unknown;

    public int Id;

    public float UpdateRadius;

    // Related to Adventurers Journal.
    public int GeometryId;

    public bool OverrideUpdateRadius;
    public bool WaitForZoneReadyPacket;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(Name);
        writer.Write(Type);

        writer.Write(Position);
        writer.Write(Rotation);

        writer.Write(Sky);

        writer.Write(Tutorial);
        writer.Write(Unknown);

        writer.Write(Id);

        writer.Write(UpdateRadius);

        writer.Write(GeometryId);

        writer.Write(OverrideUpdateRadius);
        writer.Write(WaitForZoneReadyPacket);

        return writer.Buffer;
    }
}