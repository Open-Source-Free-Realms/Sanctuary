using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// Sub-opcode 13: int SchoolId + Vector4 Position
public class FishingPacketUpdateProxiedFishingSchool : BaseFishingPacket, ISerializablePacket
{
    public new const short OpCode = 13;

    public int SchoolId;
    public Vector4 Position;

    public FishingPacketUpdateProxiedFishingSchool() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(SchoolId);
        writer.Write(Position);
        return writer.Buffer;
    }
}
