using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// Sub-opcode 10: ulong Guid + int Unknown + byte Flag1 + byte Flag2
public class FishingPacketUpdateProxiedFishingBobber : BaseFishingPacket, ISerializablePacket
{
    public new const short OpCode = 10;

    public ulong Guid;
    public int Unknown;
    public bool Flag1;
    public bool Flag2;

    public FishingPacketUpdateProxiedFishingBobber() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Guid);
        writer.Write(Unknown);
        writer.Write(Flag1);
        writer.Write(Flag2);
        return writer.Buffer;
    }
}
