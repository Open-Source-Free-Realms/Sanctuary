using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketUpdateDisposition : ISerializablePacket
{
    public const short OpCode = 35;
    public const short SubOpCode = 28;

    public ulong Guid;
    public int Disposition;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(Guid);
        writer.Write(Disposition);

        return writer.Buffer;
    }
}
