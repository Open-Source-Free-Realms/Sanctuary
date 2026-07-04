using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// Sub-opcode 8: ulong Guid + int Unknown + Vector4 Position + Vector4 Rotation
public class FishingPacketSpawnProxiedFishingBobber : BaseFishingPacket, ISerializablePacket
{
    public new const short OpCode = 8;

    public ulong Guid;
    public int Unknown;
    public Vector4 Position;
    public Vector4 Rotation;

    public FishingPacketSpawnProxiedFishingBobber() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Guid);
        writer.Write(Unknown);
        writer.Write(Position);
        writer.Write(Rotation);
        return writer.Buffer;
    }
}
