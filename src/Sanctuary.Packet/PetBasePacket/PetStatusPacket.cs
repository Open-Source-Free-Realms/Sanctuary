using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PetStatusPacket : PetBasePacket, ISerializablePacket
{
    public new const byte OpCode = 3;

    public int PetId;

    public float Hunger;

    public float Hygiene;

    public float Play;

    public float Mood;

    public PetStatusPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PetId);
        writer.Write(Hunger);
        writer.Write(Hygiene);
        writer.Write(Play);
        writer.Write(Mood);

        return writer.Buffer;
    }
}
