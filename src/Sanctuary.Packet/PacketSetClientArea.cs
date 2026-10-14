using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketSetClientArea : ISerializablePacket
{
    public const short OpCode = 89;

    public int AreaId;
    public bool Enabled;

    public int ElapsedMilliseconds;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(AreaId);
        writer.Write(Enabled);
        writer.Write(ElapsedMilliseconds);

        return writer.Buffer;
    }
}
