using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetActivePacket : PetBasePacket, ISerializablePacket
{
    public new const byte OpCode = 9;

    public int PetId;

    public ulong PetGuid;

    public PetActivePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PetId);
        writer.Write(PetGuid);

        return writer.Buffer;
    }
}
