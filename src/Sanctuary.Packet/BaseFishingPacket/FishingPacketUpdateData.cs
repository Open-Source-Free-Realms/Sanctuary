using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class FishingPacketUpdateData : BaseFishingPacket, ISerializablePacket
{
    public new const short OpCode = 1;

    public ulong Guid;
    public Vector4 Position;

    public FishingPacketUpdateData() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Position);

        return writer.Buffer;
    }
}
