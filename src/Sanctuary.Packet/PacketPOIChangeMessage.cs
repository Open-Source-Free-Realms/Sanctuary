using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketPOIChangeMessage : ISerializablePacket
{
    public const short OpCode = 104;

    public int NameId;

    public int ZoneId;

    public int AreaId;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(NameId);

        writer.Write(ZoneId);

        writer.Write(AreaId);

        return writer.Buffer;
    }
}