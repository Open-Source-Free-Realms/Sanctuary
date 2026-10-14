using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetSetNamePacket : PetBasePacket, ISerializablePacket
{
    public new const byte OpCode = 10;

    public ulong PetGuid;

    public string Name = string.Empty;

    public bool Accepted;

    public PetSetNamePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PetGuid);
        writer.Write(Name);
        writer.Write(Accepted);

        return writer.Buffer;
    }
}
