using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// Sub-opcode 12: int SchoolId (4 bytes after header)
public class FishingPacketDespawnProxiedFishingSchool : BaseFishingPacket, ISerializablePacket
{
    public new const short OpCode = 12;

    public int SchoolId;

    public FishingPacketDespawnProxiedFishingSchool() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(SchoolId);
        return writer.Buffer;
    }
}
